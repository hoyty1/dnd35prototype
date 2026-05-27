using UnityEngine;

// ============================================================================
// D&D 3.5 Combat Logger - Consistent combat message formatting
// ============================================================================

/// <summary>
/// Centralized combat logging service with consistent formatting for
/// attack results, spell failures, saving throws, miss chances, and damage.
/// All methods return formatted strings suitable for CombatUI.ShowCombatLog().
/// </summary>
public static class CombatLogger
{
    // ========================================================================
    // ATTACK RESULTS
    // ========================================================================

    /// <summary>
    /// Format an attack result message for the combat log.
    /// </summary>
    /// <param name="attackerName">Name of the attacker.</param>
    /// <param name="targetName">Name of the target.</param>
    /// <param name="d20Roll">The raw d20 roll.</param>
    /// <param name="totalRoll">Total attack roll with all modifiers.</param>
    /// <param name="targetAC">Target's armor class.</param>
    /// <param name="hit">Whether the attack hit.</param>
    /// <param name="isCritical">Whether the attack was a critical hit.</param>
    /// <param name="weaponName">Name of the weapon used (optional).</param>
    /// <returns>Formatted attack result string.</returns>
    public static string FormatAttackResult(
        string attackerName, string targetName,
        int d20Roll, int totalRoll, int targetAC,
        bool hit, bool isCritical, string weaponName = null)
    {
        string weapon = !string.IsNullOrEmpty(weaponName) ? $" ({weaponName})" : "";
        string hitStr = hit ? (isCritical ? "CRITICAL HIT" : "HIT") : "MISS";
        string icon = hit ? (isCritical ? "💥" : "⚔") : "🛡";
        return $"{icon} {attackerName} attacks {targetName}{weapon}: d20({d20Roll}) total {totalRoll} vs AC {targetAC} — {hitStr}";
    }

    // ========================================================================
    // BLINK MISS CHANCES
    // ========================================================================

    /// <summary>
    /// Format a Blink attacker miss-chance result.
    /// The blinking attacker has a 20% chance of being ethereal when attacking.
    /// </summary>
    /// <param name="attackerName">Name of the blinking attacker.</param>
    /// <param name="roll">The percentile roll (1-100).</param>
    /// <param name="missed">Whether the attack missed due to Blink.</param>
    /// <returns>Formatted Blink attacker miss message.</returns>
    public static string FormatBlinkAttackerMiss(string attackerName, int roll, bool missed)
    {
        if (missed)
            return $"🌀 {attackerName}'s attack passes through thin air! (Blink miss: rolled {roll} ≤ 20%)";
        return $"🌀 Blink attack check: {attackerName} rolled {roll} > 20% (attack proceeds).";
    }

    /// <summary>
    /// Format a Blink target concealment miss result.
    /// Attacks against a blinking target have a 50% miss chance (or 20% with See Invisible).
    /// </summary>
    /// <param name="targetName">Name of the blinking target.</param>
    /// <param name="roll">The percentile roll (1-100).</param>
    /// <param name="missChance">The miss chance percentage (50 or 20).</param>
    /// <param name="missed">Whether the attack missed due to target Blink.</param>
    /// <returns>Formatted Blink target miss message.</returns>
    public static string FormatBlinkTargetMiss(string targetName, int roll, int missChance, bool missed)
    {
        if (missed)
            return $"🌀 Attack passes through {targetName}! Target is on the Ethereal Plane. (Blink: rolled {roll} ≤ {missChance}%)";
        return $"🌀 Blink target check: {targetName} rolled {roll} > {missChance}% (attack connects).";
    }

    // ========================================================================
    // SPELL FAILURES
    // ========================================================================

    /// <summary>
    /// Format a Blink caster spell failure message (20% failure).
    /// </summary>
    /// <param name="casterName">Name of the blinking caster.</param>
    /// <param name="spellName">Name of the spell that fizzled.</param>
    /// <param name="roll">The percentile roll (1-100).</param>
    /// <param name="failed">Whether the spell failed.</param>
    /// <returns>Formatted Blink spell failure message.</returns>
    public static string FormatBlinkCasterSpellFailure(string casterName, string spellName, int roll, bool failed)
    {
        if (failed)
            return $"⚡ {casterName}'s {spellName} fizzles! (Blink spell failure: rolled {roll} ≤ 20%)";
        return $"⚡ Blink spell check: {casterName} rolled {roll} > 20% (spell proceeds).";
    }

    /// <summary>
    /// Format a Blink target spell failure message (50% failure for targeted spells).
    /// </summary>
    /// <param name="spellName">Name of the spell.</param>
    /// <param name="targetName">Name of the blinking target.</param>
    /// <param name="roll">The percentile roll (1-100).</param>
    /// <param name="failed">Whether the spell failed to reach the target.</param>
    /// <returns>Formatted Blink target spell failure message.</returns>
    public static string FormatBlinkTargetSpellFailure(string spellName, string targetName, int roll, bool failed)
    {
        if (failed)
            return $"🌀 {spellName} fails to reach {targetName}! Target is on the Ethereal Plane. (Blink: rolled {roll} ≤ 50%)";
        return $"🌀 Blink target check: {targetName} rolled {roll} > 50% (spell connects).";
    }

    /// <summary>
    /// Format an arcane spell failure from armor message.
    /// </summary>
    /// <param name="casterName">Name of the caster.</param>
    /// <param name="spellName">Name of the spell.</param>
    /// <param name="roll">The percentile roll.</param>
    /// <param name="failureChance">The arcane spell failure chance percentage.</param>
    /// <returns>Formatted arcane spell failure message.</returns>
    public static string FormatArcaneSpellFailure(string casterName, string spellName, int roll, int failureChance)
    {
        return $"⚡ {casterName}'s {spellName} fizzles! (Arcane spell failure: rolled {roll} ≤ {failureChance}%)";
    }

    // ========================================================================
    // SAVING THROWS
    // ========================================================================

    /// <summary>
    /// Format a saving throw result message.
    /// </summary>
    /// <param name="characterName">Name of the character making the save.</param>
    /// <param name="saveType">Type of save (Fortitude, Reflex, Will).</param>
    /// <param name="d20Roll">The raw d20 roll.</param>
    /// <param name="saveMod">The save modifier.</param>
    /// <param name="total">Total save result (roll + modifier).</param>
    /// <param name="dc">The save DC.</param>
    /// <param name="succeeded">Whether the save succeeded.</param>
    /// <param name="effectName">Name of the effect being saved against (optional).</param>
    /// <returns>Formatted saving throw message.</returns>
    public static string FormatSavingThrow(
        string characterName, string saveType,
        int d20Roll, int saveMod, int total, int dc,
        bool succeeded, string effectName = null)
    {
        string effect = !string.IsNullOrEmpty(effectName) ? $" vs {effectName}" : "";
        string result = succeeded ? "SUCCESS" : "FAILURE";
        string icon = succeeded ? "🛡" : "❌";
        return $"{icon} {characterName} {saveType} save{effect}: d20({d20Roll}) + {saveMod} = {total} vs DC {dc} — {result}";
    }

    // ========================================================================
    // DAMAGE
    // ========================================================================

    /// <summary>
    /// Format a damage result message.
    /// </summary>
    /// <param name="targetName">Name of the character taking damage.</param>
    /// <param name="damage">Amount of damage dealt.</param>
    /// <param name="damageType">Type of damage (e.g. "slashing", "fire").</param>
    /// <param name="sourceName">Name of the damage source (optional).</param>
    /// <param name="isNonlethal">Whether the damage is nonlethal.</param>
    /// <returns>Formatted damage message.</returns>
    public static string FormatDamage(
        string targetName, int damage, string damageType = null,
        string sourceName = null, bool isNonlethal = false)
    {
        string type = !string.IsNullOrEmpty(damageType) ? $" {damageType}" : "";
        string source = !string.IsNullOrEmpty(sourceName) ? $" from {sourceName}" : "";
        string nonlethal = isNonlethal ? " (nonlethal)" : "";
        return $"💢 {targetName} takes {damage}{type} damage{source}{nonlethal}";
    }

    /// <summary>
    /// Format a Blink area damage halving message.
    /// </summary>
    /// <param name="targetName">Name of the blinking target.</param>
    /// <param name="originalDamage">The original full damage.</param>
    /// <param name="halvedDamage">The halved damage amount.</param>
    /// <returns>Formatted Blink area damage reduction message.</returns>
    public static string FormatBlinkAreaDamageHalved(string targetName, int originalDamage, int halvedDamage)
    {
        return $"🌀 {targetName}'s Blink effect halves area damage: {originalDamage} → {halvedDamage}";
    }

    // ========================================================================
    // SPELL RESISTANCE
    // ========================================================================

    /// <summary>
    /// Format a spell resistance check result.
    /// </summary>
    /// <param name="casterName">Name of the caster.</param>
    /// <param name="targetName">Name of the target with spell resistance.</param>
    /// <param name="spellName">Name of the spell.</param>
    /// <param name="d20Roll">The raw d20 roll.</param>
    /// <param name="casterLevel">Effective caster level for the check.</param>
    /// <param name="total">Total check result.</param>
    /// <param name="srValue">Target's spell resistance value.</param>
    /// <param name="overcame">Whether the caster overcame spell resistance.</param>
    /// <returns>Formatted spell resistance check message.</returns>
    public static string FormatSpellResistance(
        string casterName, string targetName, string spellName,
        int d20Roll, int casterLevel, int total, int srValue, bool overcame)
    {
        string result = overcame ? "OVERCAME" : "BLOCKED";
        string icon = overcame ? "✨" : "🛡";
        return $"{icon} {casterName}'s {spellName} vs {targetName}'s SR {srValue}: d20({d20Roll}) + CL {casterLevel} = {total} — {result}";
    }

    // ========================================================================
    // CONCEALMENT
    // ========================================================================

    /// <summary>
    /// Format a concealment miss chance result.
    /// </summary>
    /// <param name="targetName">Name of the concealed target.</param>
    /// <param name="roll">The percentile roll (1-100).</param>
    /// <param name="missChance">The miss chance percentage.</param>
    /// <param name="missed">Whether the attack missed due to concealment.</param>
    /// <param name="concealmentSource">Source of concealment (optional, e.g. "Blur", "Darkness").</param>
    /// <returns>Formatted concealment miss message.</returns>
    public static string FormatConcealmentMiss(
        string targetName, int roll, int missChance,
        bool missed, string concealmentSource = null)
    {
        string source = !string.IsNullOrEmpty(concealmentSource) ? $" ({concealmentSource})" : "";
        if (missed)
            return $"🌫 Attack misses {targetName} due to concealment{source}! (rolled {roll} ≤ {missChance}%)";
        return $"🌫 Concealment check vs {targetName}{source}: rolled {roll} > {missChance}% (attack connects).";
    }

    // ========================================================================
    // UTILITY - Direct show to CombatUI
    // ========================================================================

    /// <summary>
    /// Log a message directly to the CombatUI if available.
    /// Convenience wrapper for GameManager.Instance access.
    /// </summary>
    /// <param name="message">The pre-formatted message to display.</param>
    public static void Show(string message)
    {
        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
        {
            GameManager.Instance.CombatUI.ShowCombatLog(message);
        }
    }
}
