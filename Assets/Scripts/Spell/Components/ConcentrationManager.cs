using UnityEngine;
using System.Collections.Generic;

public enum ConcentrationCheckType
{
    DamagedWhileCasting,
    CastingDefensively,
    Grappled,
    ViolentMotion,
    OngoingConcentration,
    HeldCharge,
    /// <summary>Casting while entangled: DC 15 + the spell's level (PHB p.308, p.70; SPL-145).</summary>
    Entangled
}
/// <summary>
/// Manages spell concentration mechanics per D&D 3.5e PHB rules.
///
/// Core Rules:
///   - A character can only concentrate on one spell at a time, and cannot cast a spell while concentrating
///     (PHB p.176): casting any spell first ends the maintained one, with no roll (GameManager.HandleConcentrationOnCasting,
///     decision D1, batch 11 step 16).
///   - Maintaining the spell is a standard action on each of the caster's turns after the casting; a turn that ends
///     without it ends the spell (<see cref="ConcentrationRules"/>, decision D2, SPL-153).
///   - A "concentration + N rounds" spell keeps its effect N more rounds however the concentration ends
///     (<see cref="ConcentrationRules.TailRounds"/>); an "up to X" spell ends at its limit.
///   - Taking damage while concentrating requires a Concentration check:
///     DC = 10 + damage dealt + spell level.
///   - Holding a charged melee touch spell and taking damage also uses
///     the same injury formula (DC = 10 + damage dealt + held spell level).
///   - Concentration can be voluntarily ended (free action).
///   - If the check fails or the character is killed/knocked unconscious,
///     concentration ends immediately (held touch charges also dissipate).
///
/// Concentration Check (D&D 3.5 PHB):
///   Roll = d20 + Concentration skill modifier
///   (Concentration ranks + CON modifier + misc bonuses)
///   Note: Combat Casting applies only to casting defensively / grapple scenarios.
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

    // ========== INITIALIZATION ==========

    /// <summary>Initialize with character references.</summary>
    public void Init(CharacterStats stats, CharacterController caster)
    {
        _stats = stats;
        Caster = caster;
        _statusEffectMgr = GetComponent<StatusEffectManager>();
    }

    /// <summary>
    /// Shared concentration check utility for in-combat spellcasting interruption checks.
    /// D&D 3.5e flow used by spellcasting AoO handling:
    ///   Roll = d20 + caster level + CON modifier (+4 Combat Casting when <see cref="CombatCastingApplies"/>).
    /// <paramref name="diceContext"/> labels the d20 (a forced-dice scenario matches it); null leaves it unlabelled. The
    /// draw is the same either way (DiceService.D20 and DiceRoller.D20 take one Random.Range(1, 21)).
    /// </summary>
    /// <summary>
    /// Whether Combat Casting's +4 counts on a casting Concentration check of <paramref name="checkType"/>: PHB p.92 gives
    /// it on checks to cast on the defensive or while grappling or pinned, so never on the entangled check (batch 11
    /// step 13). The damage, violent-motion, held-charge and ongoing checks still add it, against PHB p.92 (SPL-169).
    /// </summary>
    public static bool CombatCastingApplies(ConcentrationCheckType checkType)
    {
        return checkType != ConcentrationCheckType.Entangled;
    }

    public static ConcentrationCheckResult MakeSpellcastingConcentrationCheck(
        CharacterController caster,
        int dc,
        ConcentrationCheckType checkType,
        SpellData spell,
        int damageDealt = 0,
        string diceContext = null)
    {
        int roll = diceContext != null ? DiceService.D20(diceContext) : DiceRoller.D20();
        int bonus = 0;

        if (caster != null && caster.Stats != null)
            bonus = caster.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: CombatCastingApplies(checkType));

        int total = roll + bonus;
        bool success = total >= dc;

        return new ConcentrationCheckResult
        {
            Success = success,
            D20Roll = roll,
            TotalBonus = bonus,
            TotalRoll = total,
            DC = dc,
            SpellName = spell != null ? spell.Name : "",
            CheckType = checkType,
            DamageDealt = damageDealt
        };
    }

    public bool IsConcentrating => ConcentratingOn != null;

    /// <summary>
    /// True when the caster has kept its spell up this turn: the turn it began concentrating (the casting was the turn's
    /// standard action) or one on which it spent the standard action (ConcentrationRules.Maintain). Reset at the caster's
    /// initiative count (<see cref="OnOwnCountReached"/>); the bardic-music ConcentratedThisTurn model (CHR-026).
    /// </summary>
    public bool MaintainedThisTurn { get; private set; }

    /// <summary>The initiative counts of the caster passed since it began concentrating on the current spell.</summary>
    public int RoundsConcentrated { get; private set; }

    /// <summary>The "up to X" limit of the current spell in rounds (ConcentrationRules.MaxRounds); 0 = none.</summary>
    public int MaxRoundsForCurrent { get; private set; }

    /// <summary>The tail the last ended concentration started (ConcentrationRules.TailRounds), 0 when none.</summary>
    public int LastTailRounds { get; private set; }

    /// <summary>Marks this turn's concentration as kept (ConcentrationRules.Maintain spends the standard action).</summary>
    public void MarkMaintained()
    {
        if (IsConcentrating)
            MaintainedThisTurn = true;
    }
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

        // If already concentrating, the previous spell ends (or runs its tail): the caster ceased concentrating on it to
        // cast this one (PHB p.176). The cast core normally ended it already (HandleConcentrationOnCasting).
        if (IsConcentrating)
        {
            string prevLog = CeaseConcentration("began concentrating on " + effect.Spell.Name);
            if (!string.IsNullOrEmpty(prevLog))
                log += prevLog + "\n";
        }

        ConcentratingOn = effect;
        MaintainedThisTurn = true; // the casting was this turn's standard action (PHB p.176)
        RoundsConcentrated = 0;
        MaxRoundsForCurrent = ConcentrationRules.MaxRounds(effect.Spell, effect.CasterLevel);
        log += $"🔵 {_stats.CharacterName} concentrates on {effect.Spell.Name}.";
        Debug.Log($"[Concentration] {_stats.CharacterName} begins concentrating on {effect.Spell.Name}");

        return log;
    }

    /// <summary>
    /// End the concentration and remove the spell's effect outright, with no "+N rounds" tail: the slot reuse and the
    /// removal of the creature the effect sits on. A caster that stops concentrating uses <see cref="CeaseConcentration"/>.
    /// </summary>
    /// <param name="silent">If true, don't log.</param>
    /// <returns>Log message describing what happened.</returns>
    public string EndConcentration(bool silent = false)
    {
        if (!IsConcentrating) return "";

        string spellName = ConcentrationSpellName;
        EndInternal(runTail: false);

        string log = "";
        if (!silent)
        {
            log = $"🔴 {_stats.CharacterName} ends concentration on {spellName}.";
            Debug.Log($"[Concentration] {_stats.CharacterName} ends concentration on {spellName}");
        }

        return log;
    }

    /// <summary>
    /// The caster stops concentrating (to cast another spell, at the end of a turn it did not keep the spell up, or by
    /// choice, a free action; PHB p.176, p.141): the spell ends, or a "concentration + N rounds" spell keeps its effect N
    /// more rounds (<see cref="ConcentrationRules.TailRounds"/>). Returns the log line.
    /// </summary>
    public string CeaseConcentration(string reason, bool silent = false)
    {
        if (!IsConcentrating) return "";

        string spellName = ConcentrationSpellName;
        int tail = EndInternal(runTail: true);
        if (silent)
            return "";
        string why = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
        string log = tail > 0
            ? $"🔴 {_stats.CharacterName} stops concentrating on {spellName}{why}; it lasts {tail} more round{(tail == 1 ? "" : "s")}."
            : $"🔴 {_stats.CharacterName} stops concentrating on {spellName}{why}; it ends.";
        Debug.Log($"[Concentration] {log}");
        return log;
    }

    /// <summary>
    /// The caster's initiative count was reached: the new turn owes a new concentration, and a spell at its "up to X"
    /// limit ends (PHB p.176; no tail, its whole duration has run). Returns the log line or an empty string.
    /// </summary>
    public string OnOwnCountReached()
    {
        if (!IsConcentrating)
            return "";
        MaintainedThisTurn = false;
        RoundsConcentrated++;
        int max = MaxRoundsForCurrent;
        if (max <= 0 || RoundsConcentrated < max)
            return "";
        string spellName = ConcentrationSpellName;
        EndInternal(runTail: false);
        string log = $"⏱ {_stats.CharacterName}'s {spellName} reaches its duration limit ({max} rounds) and ends.";
        Debug.Log($"[Concentration] {log}");
        return log;
    }

    /// <summary>
    /// Clears the concentration and either starts the spell's tail on its effect (the effect stays where it is and ticks
    /// at the caster's initiative count, CMB-006) or removes the effect. Returns the tail rounds started (0 when none).
    /// </summary>
    private int EndInternal(bool runTail)
    {
        var effect = ConcentratingOn;
        ConcentratingOn = null;
        MaintainedThisTurn = false;
        RoundsConcentrated = 0;
        MaxRoundsForCurrent = 0;
        LastTailRounds = 0;
        if (effect == null)
            return 0;

        int tail = runTail ? ConcentrationRules.TailRounds(effect.Spell, effect.CasterLevel) : 0;
        LastTailRounds = tail;
        if (tail > 0)
        {
            StatusEffectManager holder = FindEffectHolder(effect);
            if (holder != null)
            {
                effect.RemainingRounds = tail;
                effect.DurationAnchor = Caster;
            }
            // No holder: an effect kept elsewhere (the Summon Swarm marker); its owner runs the tail (GameManager).
            return tail;
        }

        // Remove the effect from whoever it was applied to
        RemoveConcentrationEffect(effect);
        return 0;
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
        int dc = ConcentrationService.GetDamageDC(damageTaken, spellLevel);

        return PerformConcentrationCheck(dc, $"taking {damageTaken} damage");
    }

    /// <summary>
    /// Perform a concentration check to maintain a held melee touch spell charge after taking damage.
    /// Uses PHB injury formula: DC = 10 + damage dealt + held spell level.
    /// </summary>
    public ConcentrationCheckResult CheckHeldChargeOnDamage(SpellData heldSpell, int damageTaken)
    {
        if (heldSpell == null)
            return new ConcentrationCheckResult { Success = true, LogMessage = "" };

        int dc = ConcentrationService.GetDamageDC(damageTaken, Mathf.Max(0, heldSpell.SpellLevel));

        int concentrationBonus = GetConcentrationBonus();
        int d20 = DiceRoller.D20();
        int totalRoll = d20 + concentrationBonus;
        bool success = totalRoll >= dc;

        string bonusBreakdown = GetBonusBreakdown();
        string resultStr = success ? "<color=#88FF88>Success!</color>" : "<color=#FF4444>Failed!</color>";

        var result = new ConcentrationCheckResult
        {
            Success = success,
            D20Roll = d20,
            TotalBonus = concentrationBonus,
            TotalRoll = totalRoll,
            DC = dc,
            SpellName = heldSpell.Name,
            LogMessage = $"⚡ Concentration check (held {heldSpell.Name}) — DC {dc} (taking {damageTaken} damage)\n" +
                        $"  Rolls d20({d20}) {bonusBreakdown} = {totalRoll}. {resultStr}\n" +
                        (success
                            ? $"  ✅ Held charge maintained ({heldSpell.Name})."
                            : $"  ❌ Held charge lost ({heldSpell.Name}).")
        };

        return result;
    }

    /// <summary>
    /// Perform a concentration check for vigorous motion (e.g., riding).
    /// DC = 10 + spell level.
    /// </summary>
    public ConcentrationCheckResult CheckConcentrationVigorousMotion()
    {
        if (!IsConcentrating)
            return new ConcentrationCheckResult { Success = true, LogMessage = "" };

        int dc = ConcentrationService.GetVigorousMotionDC(ConcentrationSpellLevel);
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

        int dc = ConcentrationService.GetViolentMotionDC(ConcentrationSpellLevel);
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

        int dc = ConcentrationService.GetEntangledCastingDC(ConcentrationSpellLevel);
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
        int tail = EndInternal(runTail: true); // a "+N rounds" spell keeps going (delegated RAW reading 2026-10-15)

        string log = $"💥 {_stats.CharacterName}'s concentration on {spellName} is broken ({reason})!"
            + (tail > 0 ? $" It lasts {tail} more round{(tail == 1 ? "" : "s")}." : "");
        Debug.Log($"[Concentration] {log}");
        return log;
    }

    // ========== PRIVATE HELPERS ==========

    /// <summary>
    /// Perform the actual concentration check roll.
    /// D&D 3.5 PHB: d20 + concentration modifier (ranks + CON + misc).
    /// </summary>
    private ConcentrationCheckResult PerformConcentrationCheck(int dc, string reason)
    {
        string spellName = ConcentrationSpellName;

        // D&D 3.5 PHB concentration check: d20 + concentration modifier
        // (ranks + CON modifier + misc).
        int concentrationBonus = GetConcentrationBonus();

        int d20 = DiceRoller.D20();
        int totalRoll = d20 + concentrationBonus;
        bool success = totalRoll >= dc;

        // Build detailed log message
        string bonusBreakdown = GetBonusBreakdown();
        string resultStr = success ? "<color=#88FF88>Success!</color>" : "<color=#FF4444>Failed!</color>";

        var result = new ConcentrationCheckResult();
        result.Success = success;
        result.D20Roll = d20;
        result.TotalBonus = concentrationBonus;
        result.TotalRoll = totalRoll;
        result.DC = dc;
        result.SpellName = spellName;

        string logLine1 = $"⚡ Concentration check ({spellName}) — DC {dc} ({reason})";
        string logLine2 = $"  Rolls d20({d20}) {bonusBreakdown} = {totalRoll}. {resultStr}";

        if (success)
        {
            result.LogMessage = $"{logLine1}\n{logLine2}\n  ✅ Concentration maintained on {spellName}.";
            Debug.Log($"[Concentration] {_stats.CharacterName}: Check DC {dc} — rolled {totalRoll} — SUCCESS (maintaining {spellName})");
        }
        else
        {
            // Concentration fails: the spell ends, or runs its "+N rounds" tail (PHB p.176; delegated RAW reading 2026-10-15)
            int tail = EndInternal(runTail: true);
            result.LogMessage = $"{logLine1}\n{logLine2}\n  ❌ Concentration on {spellName} is broken!"
                + (tail > 0 ? $" It lasts {tail} more round{(tail == 1 ? "" : "s")}." : "");
            Debug.Log($"[Concentration] {_stats.CharacterName}: Check DC {dc} — rolled {totalRoll} — FAILED ({spellName} ends)");
        }

        return result;
    }

    /// <summary>
    /// Get the total Concentration skill bonus.
    /// D&D 3.5e PHB: Concentration modifier = ranks + CON modifier (+misc).
    /// </summary>
    private int GetConcentrationBonus()
    {
        if (_stats == null) return 0;

        int ranks = 0;
        if (_stats.Skills.ContainsKey("Concentration"))
            ranks = _stats.Skills["Concentration"].Ranks;

        int conMod = CharacterStats.GetModifier(_stats.CON);
        return ranks + conMod;
    }

    /// <summary>Build a human-readable breakdown of the concentration bonus.</summary>
    private string GetBonusBreakdown()
    {
        var parts = new List<string>();

        int ranks = 0;
        if (_stats != null && _stats.Skills.ContainsKey("Concentration"))
            ranks = _stats.Skills["Concentration"].Ranks;

        int conMod = _stats != null ? CharacterStats.GetModifier(_stats.CON) : 0;

        if (ranks > 0) parts.Add($"+ {ranks}(ranks)");
        if (conMod != 0) parts.Add($"+ {conMod}(CON)");

        return parts.Count > 0 ? string.Join(" ", parts) : "";
    }

    /// <summary>The StatusEffectManager that holds <paramref name="effect"/> (this character's or another's), or null.</summary>
    private StatusEffectManager FindEffectHolder(ActiveSpellEffect effect)
    {
        if (effect == null)
            return null;
        if (_statusEffectMgr != null && _statusEffectMgr.ActiveEffects.Contains(effect))
            return _statusEffectMgr;
        foreach (var mgr in FindObjectsByType<StatusEffectManager>(FindObjectsInactive.Exclude))
            if (mgr.ActiveEffects.Contains(effect))
                return mgr;
        return null;
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

    /// <summary>Spellcasting interruption check category.</summary>
    public ConcentrationCheckType CheckType = ConcentrationCheckType.OngoingConcentration;

    /// <summary>Damage dealt that contributed to DC (if any).</summary>
    public int DamageDealt;

    // Compatibility aliases for newer call sites.
    public int Roll => D20Roll;
    public int Bonus => TotalBonus;
    public int Total => TotalRoll;
}