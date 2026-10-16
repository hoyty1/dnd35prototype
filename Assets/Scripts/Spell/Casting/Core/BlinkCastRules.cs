using UnityEngine;

/// <summary>
/// The Blink spell's two spell-failure rolls (PHB p.206), the one rule every cast path uses through the cast core
/// (GameManager.CastCore.cs: the Failure stage's <see cref="TryCasterFailure"/> and the TargetBlink stage's
/// <see cref="TryTargetFailure"/>, batch 11 step 13, the same for every caster) and the direct item use
/// (GameManager.TryUseConsumableFromInventory). Folded into the cast core from the old SpellResolutionService by batch 11
/// step 29 (SPL-056); its spell resistance wrapper went with it (the one check is SpellSaveResolver.RollSpellResistance,
/// a cast's own through CastContext.RollSpellResistance). The dice labels are the cast core's: forced-dice scenarios
/// and tests match them.
/// </summary>
public static class BlinkCastRules
{
    /// <summary>The dice label of the blinking caster's 20% roll.</summary>
    public const string CasterDiceContext = "Blink caster spell failure";

    /// <summary>The dice label of the 50% roll of a spell aimed at a blinking creature.</summary>
    public const string TargetDiceContext = "Blink target spell failure";

    /// <summary>
    /// The blinking caster's 20% spell failure (PHB p.206: a spell may go off on the Ethereal Plane). Rolled apart from
    /// arcane spell failure. No roll (<paramref name="roll"/> 0) for a null or non-blinking caster.
    /// </summary>
    /// <returns>True when the spell fails.</returns>
    public static bool TryCasterFailure(CharacterController caster, string spellName, out int roll)
    {
        roll = 0;

        if (caster == null || !caster.HasActiveBlinkEffect)
            return false;

        roll = DiceService.Percentile(CasterDiceContext);

        if (roll <= 20)
        {
            Debug.Log($"[Blink] Caster failure: {caster.Stats?.CharacterName}'s {spellName} fizzles (rolled {roll} ≤ 20%)");
            return true;
        }

        Debug.Log($"[Blink] Caster check passed: {caster.Stats?.CharacterName} rolled {roll} > 20%");
        return false;
    }

    /// <summary>
    /// The 50% chance that an individually targeted spell fails against a blinking creature (PHB p.206). No roll
    /// (<paramref name="roll"/> 0) for a null target, the caster itself, a non-blinking target, or a Self or Area spell.
    /// A ray aimed at one creature counts as individually targeted (delegated RAW reading 2026-10-14, RULES_COVERAGE
    /// 4.1). PHB p.206 exempts an attacker that can target invisible, ethereal creatures; that exception is not built
    /// (SPL-199).
    /// </summary>
    /// <returns>True when the spell fails to reach the target.</returns>
    public static bool TryTargetFailure(CharacterController caster, CharacterController target, SpellData spell, out int roll)
    {
        roll = 0;

        if (target == null || target == caster || !target.HasActiveBlinkEffect)
            return false;

        // Not an individually targeted spell: a Self or an Area spell.
        if (spell.TargetType == SpellTargetType.Self || spell.TargetType == SpellTargetType.Area)
            return false;

        string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
        roll = DiceService.Percentile(TargetDiceContext);

        if (roll <= 50)
        {
            Debug.Log($"[Blink] Target failure: {spell.Name} fails to reach {targetName} (rolled {roll} ≤ 50%)");
            return true;
        }

        Debug.Log($"[Blink] Target check passed: {targetName} rolled {roll} > 50%");
        return false;
    }
}
