using System;
using System.Collections.Generic;
using DND35e.Identifiers;

/// <summary>
/// Which landed single-target casts reach <c>GameManager.ApplySpellBuff</c>, the shared effect step of the PC
/// pipeline (<c>PerformSpellCast</c>) and the NPC cast executor (<c>TryNPCPerformSpellCast</c>) (SPL-037).
///
/// Every Buff, Debuff, Control, Illusion and Wall spell does. A spell of another effect type reaches it only when
/// <c>ApplySpellBuff</c> has a dedicated branch for it: those are listed here, so both pipelines route them the same
/// way (docs/systems/PC_NPC_PARITY.md). A listed Healing or Damage spell sets <c>HealingResolvedByHandler</c> or
/// <c>DamageResolvedByHandler</c>, so <c>SpellCaster.Cast</c> does not heal or damage the target a second time.
/// The PC area pipeline (<c>PerformAoESpellCast</c>) routes only the tracked effect types
/// (<see cref="IsTrackedEffectType"/>) to ApplySpellBuff for each target, not the single-target extras listed here.
/// </summary>
public static class SpellEffectRouting
{
    private static readonly HashSet<string> HandlerOwnedSpellIds = new HashSet<string>(StringComparer.Ordinal)
    {
        // Utility (Passwall is Area with no AoE shape, so it takes the single-target flow, as SPL-042 describes)
        SpellNames.CONTINUAL_FLAME,
        SpellNames.SHRINK_ITEM,
        SpellNames.DANCING_LIGHTS,
        SpellNames.PASSWALL,
        // Divination (the alignment and undead detection spells are matched by AlignmentDetectionEffectData.IsDetectionSpell)
        SpellNames.TRUE_SEEING,
        SpellNames.SEE_INVISIBLE,
        // Dispel (SpellCategoryClassifier reclassifies these at init)
        SpellNames.DISPEL_MAGIC,
        SpellNames.BREAK_ENCHANTMENT,
        SpellNames.REMOVE_FEAR,
        // Healing (HealingResolvedByHandler)
        SpellNames.HEAL,
        SpellNames.RESURRECTION,
        SpellNames.RESTORATION,
        SpellNames.GREATER_RESTORATION,
        SpellNames.STONE_TO_FLESH,
        // Damage (DamageResolvedByHandler)
        SpellNames.DISINTEGRATE,
    };

    /// <summary>The effect types whose landed casts always go through ApplySpellBuff.</summary>
    public static bool IsTrackedEffectType(SpellEffectType effectType)
    {
        return effectType == SpellEffectType.Buff
            || effectType == SpellEffectType.Debuff
            || effectType == SpellEffectType.Control
            || effectType == SpellEffectType.Illusion
            || effectType == SpellEffectType.Wall;
    }

    /// <summary>
    /// True when ApplySpellBuff has a dedicated branch for the spell, listed here because its effect type (after
    /// SpellCategoryClassifier.ReclassifyAll) is not a tracked one.
    /// </summary>
    public static bool HasDedicatedHandlerOutsideTrackedTypes(string spellId)
    {
        return !string.IsNullOrEmpty(spellId)
            && (HandlerOwnedSpellIds.Contains(spellId) || AlignmentDetectionEffectData.IsDetectionSpell(spellId));
    }

    /// <summary>True when a landed single-target cast of <paramref name="spell"/> goes through ApplySpellBuff.</summary>
    public static bool ReachesApplySpellBuff(SpellData spell)
    {
        if (spell == null)
            return false;
        return IsTrackedEffectType(spell.EffectType) || HasDedicatedHandlerOutsideTrackedTypes(spell.SpellId);
    }

    /// <summary>The spells listed above, for tests and docs.</summary>
    public static IEnumerable<string> HandlerOwnedSpellIdsForTests => HandlerOwnedSpellIds;
}
