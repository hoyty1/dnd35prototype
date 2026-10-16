using System;
using UnityEngine;

// ============================================================================
// D&D 3.5 Metamagic System - MetamagicModifier & MetamagicSystem
// ============================================================================
// MetamagicSystem: the live metamagic legality and casting-action rule of every cast (batch 11 step 21, SPL-055).
// MetamagicModifier: one feat applied by a feat or a rod, used by MetamagicRodActivation (rods are data only, ITM-018).
// ============================================================================

/// <summary>
/// Represents a single metamagic modifier applied to a spell.
/// Wraps a MetamagicFeatId with additional context (rod source, heighten level).
/// </summary>
[Serializable]
public class MetamagicModifier
{
    /// <summary>Which metamagic feat this modifier represents.</summary>
    public MetamagicFeatId Type;

    /// <summary>
    /// Spell slot level increase for this metamagic.
    /// Computed from the feat type (e.g., Empower = +2, Maximize = +3).
    /// For Heighten, this is (target level - base level).
    /// </summary>
    public int SlotIncrease;

    /// <summary>
    /// True if this metamagic was applied by a Metamagic Rod (not a feat).
    /// Rod-applied metamagics do NOT increase the spell slot level consumed.
    /// </summary>
    public bool AppliedByRod;

    /// <summary>For Heighten Spell: the target spell level to heighten to.</summary>
    public int HeightenToLevel = -1;

    /// <summary>
    /// Create a metamagic modifier from a feat.
    /// </summary>
    public MetamagicModifier(MetamagicFeatId type, int baseSpellLevel, bool appliedByRod = false)
    {
        Type = type;
        AppliedByRod = appliedByRod;
        SlotIncrease = MetamagicData.GetStandardLevelAdjustment(type);
    }

    /// <summary>
    /// Create a Heighten Spell modifier with a specific target level.
    /// </summary>
    public static MetamagicModifier CreateHeighten(int baseSpellLevel, int targetLevel, bool appliedByRod = false)
    {
        return new MetamagicModifier(MetamagicFeatId.HeightenSpell, baseSpellLevel, appliedByRod)
        {
            HeightenToLevel = targetLevel,
            SlotIncrease = Mathf.Max(0, targetLevel - baseSpellLevel)
        };
    }
}

/// <summary>
/// The metamagic rules of a cast, one for every caster and path (batch 11 step 21, metamagic-legality-and-slots;
/// docs/designs/cast_core_batch11.md; SPL-055): the cast core's Legality stage asks <see cref="ValidateMetamagicApplication"/>
/// (GameManager.CheckCastMetamagicLegality) and its Cost stage charges <see cref="GetMetamagicCastingAction"/> (through
/// CastActionRules.CastingAction, which adds the item and held-charge costs); the PC spell menu checks the same rules before
/// targeting (CastActionRules.Refusal) and the AI's spell choice skips what they refuse, so PCs and NPCs are bound alike
/// (docs/systems/PC_NPC_PARITY.md). Pure: no side effects. The metamagic's data changes are
/// SpellCaster.ApplyMetamagicToSpellData's; the slot it spends is SpellcastingComponent's (CastWizardSpellWithMetamagic,
/// SpontaneousCastFromSlot).
///
/// RAW applied:
/// - PHB p.88 (Metamagic Feats: Wizards and Divine Spellcasters; Sorcerers and Bards; Spontaneous Casting and Metamagic
///   Feats): a prepared metamagic spell is cast in its normal casting time; a spontaneous caster, or a cleric converting
///   to a cure or inflict spell, takes a full-round action for a 1-action metamagic spell. The cast's source decides,
///   not the caster's classes (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1; the source is still picked per
///   caster, CastSource.SlotKindFor, SPL-159), and a free- or swift-action spell cast spontaneously with metamagic is a
///   full-round action too (delegated RAW reading 2026-10-14). A longer casting time takes an extra full round, which
///   the game cannot express (SPL-167): it costs the full round.
/// - PHB p.98 (Quicken Spell): a quickened spell is a free action; the feat cannot be applied to a spell cast
///   spontaneously (Special).
/// - PHB p.88 and the feats' entries (PHB p.93-101): the caster needs the feat; the adjusted level is at most 9th; a
///   heightened spell has a higher level than normal (PHB p.95), so a Heighten that raises nothing is refused. A feat
///   cannot be applied to a spell it cannot modify: PHB p.88 says metamagic feats cannot be used with all spells and
///   sends the reader to each feat's list of the spells it can't modify, the "not affected" spells of Empower (p.93),
///   Extend and Enlarge (p.94), Maximize (p.97) and Widen (p.102); the test is MetamagicData.IsApplicable, the same
///   test the metamagic panel and the preparation screen offer by, so a Scripted or AI cast is refused what a PC is
///   never offered (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1). Silent Spell is refused on a bard spell (PHB
///   p.100, Special; <see cref="IsBardSpell"/>, since the batch 11 step 28 review). IsApplicable does not yet test Silent
///   or Still on a spell without that component (PHB p.100, p.101) (SPL-184).
///   Quicken's casting-time limit (PHB p.98) is not tested yet (SPL-098).
/// - Items and the F12 panel: a scroll or wand carries the metamagic it was made with (DMG p.238, p.245; its maker had
///   the feat), so its cast is not tested; a held charge's delivery was tested at the hold (no casting, PHB p.141); the
///   F12 panel's toggles stand in for the feats (a developer tool, docs/TESTING.md 4.2), the other rules bind it.
/// - Metamagic rods (DMG p.233) apply a feat the caster lacks without raising the level
///   (MetamagicData.RodAppliedMetamagic): the feat test and the level cap skip a rod's feat; no cast path uses a rod yet
///   (ITM-018).
/// </summary>
public static class MetamagicSystem
{
    /// <summary>Maximum effective spell level in D&D 3.5e.</summary>
    public const int MaxSpellLevel = 9;

    public const string RefusalQuickenSpontaneous = "quicken-spontaneous";
    public const string RefusalMissingFeat = "metamagic-missing-feat";
    public const string RefusalAboveNinth = "metamagic-above-9th";
    public const string RefusalHeightenNoRaise = "metamagic-heighten-no-raise";
    public const string RefusalNotApplicable = "metamagic-not-applicable";

    /// <summary>True when <paramref name="metamagic"/> quickens the cast (a held charge's delivery is not a casting, PHB p.141).</summary>
    public static bool IsQuickened(MetamagicData metamagic, CastSource source)
    {
        return metamagic != null && metamagic.Has(MetamagicFeatId.QuickenSpell)
               && (source == null || source.Kind != CastSourceKind.HeldChargeDelivery);
    }

    /// <summary>
    /// Why <paramref name="metamagic"/> may not be applied to this cast of <paramref name="spell"/>, or null (see the class
    /// summary): Quicken Spell on a spontaneous cast, a Heighten that raises nothing, a feat that cannot modify the spell
    /// (PHB p.88), a feat the caster lacks (tested only when <paramref name="casterStats"/> is given), an adjusted level
    /// above 9th.
    /// No metamagic, a scroll, a wand and a held charge's delivery always pass.
    /// </summary>
    public static string ValidateMetamagicApplication(SpellData spell, MetamagicData metamagic,
        CharacterStats casterStats = null, CastSource source = null)
    {
        if (spell == null || metamagic == null || !metamagic.HasAnyMetamagic)
            return null;
        if (source != null && (source.IsItem || source.Kind == CastSourceKind.HeldChargeDelivery))
            return null;

        if (IsQuickened(metamagic, source) && source != null && source.IsSpontaneous)
            return RefusalQuickenSpontaneous;

        if (metamagic.Has(MetamagicFeatId.HeightenSpell) && metamagic.HeightenToLevel <= spell.SpellLevel)
            return RefusalHeightenNoRaise;

        // PHB p.88: a feat cannot be used with a spell its entry says it cannot modify (delegated RAW reading 2026-10-14).
        // The cast's clone already carries the metamagic's changes (SpellCaster.ApplyMetamagicToSpellData), which leave
        // every input of IsApplicable's tests as it was (effect type, duration kind, range, area).
        foreach (MetamagicFeatId feat in metamagic.AppliedMetamagic)
        {
            if (!MetamagicData.IsApplicable(feat, spell))
                return RefusalNotApplicable;
        }

        // PHB p.100 (Silent Spell, Special): a bard spell cannot be enhanced by Silent Spell. A bard spell is a spontaneous
        // cast whose casting class is Bard (SpellSaveDCRules.ResolveCastingClass, the class its save DC uses; the source
        // carries no class yet, SPL-159). Batch 11 step 28 review (2026-10-16), SPL-184 item 1 in part.
        if (metamagic.Has(MetamagicFeatId.SilentSpell) && IsBardSpell(spell, casterStats, source))
            return RefusalNotApplicable;

        bool featsSupplied = source != null && source.Kind == CastSourceKind.TestPanel;
        if (casterStats != null && !featsSupplied)
        {
            foreach (MetamagicFeatId feat in metamagic.AppliedMetamagic)
            {
                if (metamagic.IsFromRod(feat))
                    continue;
                if (!casterStats.HasFeat(MetamagicData.GetFeatName(feat)))
                    return RefusalMissingFeat;
            }
        }

        if (spell.SpellLevel + metamagic.GetFeatOnlyLevelAdjustment(spell.SpellLevel) > MaxSpellLevel)
            return RefusalAboveNinth;
        return null;
    }

    /// <summary>
    /// True when this cast of <paramref name="spell"/> is a bard spell (PHB p.100, Silent Spell): a cast from a spontaneous
    /// spell per day (or a charge held from one) by a caster whose casting class for the spell is Bard. False without the
    /// caster's stats or the source.
    /// </summary>
    public static bool IsBardSpell(SpellData spell, CharacterStats casterStats, CastSource source)
    {
        if (spell == null || casterStats == null || source == null || !source.IsSpontaneous
            || source.Kind == CastSourceKind.SpontaneousConversion)
            return false;
        return string.Equals(SpellSaveDCRules.ResolveCastingClass(casterStats, spell), "Bard", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The action a cast of <paramref name="spell"/> from a slot, a conversion, a hold or the F12 panel takes with
    /// <paramref name="metamagic"/> (see the class summary): a free action when quickened (PHB p.98); a full-round action
    /// for any metamagic cast whose source is spontaneous (PHB p.88); else the spell's casting time (SpellData.ActionType,
    /// PHB p.174). A scroll, a wand and a held charge's delivery cost their own action: CastActionRules.CastingAction
    /// answers for them and asks this for every other source.
    /// </summary>
    public static CastActionCost GetMetamagicCastingAction(SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (IsQuickened(metamagic, source))
            return CastActionCost.Free;

        // PHB p.88: a metamagic spell cast spontaneously takes a full-round action, a free- or swift-action spell too
        // (delegated RAW reading 2026-10-14); for a 1-round spell an extra full round, which costs the full round here
        // (SPL-167).
        if (metamagic != null && metamagic.HasAnyMetamagic && source != null && source.IsSpontaneous)
            return CastActionCost.FullRound;

        SpellActionType dataAction = spell != null ? spell.ActionType : SpellActionType.Standard;
        if (dataAction == SpellActionType.Free)
            return CastActionCost.Free;
        if (dataAction == SpellActionType.Swift)
            return CastActionCost.Swift;
        return dataAction == SpellActionType.FullRound ? CastActionCost.FullRound : CastActionCost.Standard;
    }

    /// <summary>The player-facing text of a <see cref="ValidateMetamagicApplication"/> code, or null for another code.</summary>
    public static string DescribeRefusal(string refusal, string casterName, string spellName, MetamagicData metamagic)
    {
        string feats = "metamagic";
        if (metamagic != null && metamagic.HasAnyMetamagic)
        {
            var ordered = new System.Collections.Generic.List<MetamagicFeatId>(metamagic.AppliedMetamagic);
            ordered.Sort((a, b) => ((int)a).CompareTo((int)b));
            feats = string.Join(" and ", ordered.ConvertAll(MetamagicData.GetDisplayName));
        }
        switch (refusal)
        {
            case RefusalQuickenSpontaneous:
                return $"{casterName} cannot quicken {spellName}: Quicken Spell cannot be applied to a spell cast spontaneously (PHB p.98).";
            case RefusalMissingFeat:
                return $"{casterName} cannot cast {spellName} with {feats}: the caster does not have the metamagic feat (PHB p.88).";
            case RefusalNotApplicable:
                return $"{casterName} cannot cast {spellName} with {feats}: the feat cannot modify this spell (PHB p.88 and the feat's entry).";
            case RefusalHeightenNoRaise:
                return $"{casterName} cannot heighten {spellName} without raising its level (PHB p.95).";
            case RefusalAboveNinth:
                return $"{casterName} cannot cast {spellName} with {feats}: its adjusted level would be above 9th (PHB p.88).";
            default:
                return null;
        }
    }
}
