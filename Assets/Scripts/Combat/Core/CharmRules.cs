using System;
using System.Collections.Generic;

/// <summary>
/// The charm link (batch 13 step 20, decision D14; explicit RAW, PHB p.209 Charm Person, Charm Monster on the same page):
/// a charmed creature regards its charmer as a trusted friend and ally, so it makes no attack, hostile maneuver or hostile
/// spell against it, and no attack of opportunity either (an attack of opportunity is an attack: delegated RAW reading
/// 2026-10-29, D14). One pure predicate for every creature, PC or NPC: the shared direct-attack gate
/// (AttackLegality, <see cref="AttackLegalityChecks.Charm"/>), the attack-of-opportunity gates (ThreatSystem), the AI's
/// target choice (AIService.SelectBestTarget) and the legal set (ActorActionSet) all read it. No dice, no log.
/// </summary>
public static class CharmRules
{
    /// <summary>
    /// True when <paramref name="charmed"/> is charmed by <paramref name="candidate"/>: one of its Charmed conditions has
    /// the candidate as its source (the condition's source, else the charm payload's caster, else the source's name when
    /// neither resolves to a creature).
    /// </summary>
    public static bool IsCharmedBy(CharacterController charmed, CharacterController candidate)
    {
        if (charmed == null || candidate == null || charmed == candidate || charmed.Stats == null)
            return false;
        if (!charmed.HasCondition(CombatConditionType.Charmed))
            return false;

        GameManager gm = GameManager.Instance;
        if (gm == null)
            return IsCharmedByName(charmed, candidate);

        List<ConditionService.ActiveCondition> active = gm.GetActiveConditions(charmed);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Charmed)
                continue;

            CharacterController source = condition.Source;
            if (source == null && condition.Data is CharmedConditionData charmData)
                source = charmData.Caster;

            if (source != null && source == candidate)
                return true;

            if (source == null
                && !string.IsNullOrWhiteSpace(condition.SourceName)
                && candidate.Stats != null
                && string.Equals(condition.SourceName, candidate.Stats.CharacterName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Without a GameManager (an edit-mode suite): a Charmed condition on the creature's own list naming the candidate.</summary>
    private static bool IsCharmedByName(CharacterController charmed, CharacterController candidate)
    {
        List<StatusEffect> effects = charmed.GetActiveConditions();
        if (effects == null || candidate.Stats == null)
            return false;
        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffect effect = effects[i];
            if (effect != null && ConditionRules.Normalize(effect.Type) == CombatConditionType.Charmed
                && string.Equals(effect.SourceName, candidate.Stats.CharacterName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>The refusal text of an attack on the charmer (PHB p.209).</summary>
    public static string Refusal(CharacterController charmed, CharacterController charmer)
        => $"{Name(charmed)} is charmed by {Name(charmer)} and makes no attack on its charmer (PHB p.209)";

    private static string Name(CharacterController c) => c != null && c.Stats != null ? c.Stats.CharacterName : "the creature";
}
