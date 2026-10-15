/// <summary>The action a cast charges (<see cref="CastActionRules.CastingAction"/>).</summary>
public enum CastActionCost
{
    /// <summary>
    /// Nothing at the cast: a scroll or wand charges the item's own action when the item is spent
    /// (GameManager.ConsumeScrollAfterCast, ConsumeWandChargeAfterCast; ITM-005), and a held-charge delivery is free
    /// (LegacyCastQuirk.HeldChargeDeliveryIsFree, SPL-156). For an item this is not the whole RAW cost: activating a
    /// spell's power in a scroll or wand takes the spell's casting time (DMG p.213), so a 1-round spell from an item is
    /// a full-round action and a Feather Fall a free action (ITM-086).
    /// </summary>
    ItemOrDelivery,

    /// <summary>A free action: a quickened spell (PHB p.98) or a spell whose casting time is a free action (Feather Fall, PHB p.174, p.229).</summary>
    Free,

    /// <summary>A swift action (no spell in the database has one).</summary>
    Swift,

    /// <summary>A standard action (casting time 1 standard action, PHB p.174).</summary>
    Standard,

    /// <summary>
    /// A full-round action: a 1-round casting time (PHB p.174), or a spontaneous cast with a metamagic feat (PHB p.88).
    /// </summary>
    FullRound
}

/// <summary>
/// The action-cost rules of a cast (batch 11 step 12, raw-action-cost-provocation; docs/designs/cast_core_batch11.md),
/// one rule for every caster and path: the cast core's Legality and Cost stages read it, the PC spell menu checks it
/// before targeting and the AI's spell choice skips what it refuses (AIService.SelectSpell), so PCs and NPCs are bound
/// alike (docs/systems/PC_NPC_PARITY.md). Pure: no side effects.
///
/// RAW applied:
/// - PHB p.98 (Quicken Spell): a quickened spell is a free action and does not provoke; one quickened spell per round;
///   the feat cannot be applied to a spell cast spontaneously.
/// - PHB p.88 (Casting Time; Spontaneous Casting and Metamagic Feats): a spontaneous cast with a metamagic feat takes a
///   full-round action for a 1-action spell; the source of the cast decides, not the caster's classes (delegated RAW
///   reading 2026-10-14; the source is still picked per caster, CastSource.SlotKindFor, SPL-159). A 1-action spell
///   includes one whose casting time is a free or swift action (delegated RAW reading 2026-10-14: PHB p.88 says the
///   spontaneous caster must take more time for any metamagic spell, and p.98 that the metamagic raises the casting
///   time to a full-round action), so such a cast is a full-round action and no longer the round's free-action spell.
///   A spell with a longer casting time takes an extra full-round action, which the game cannot express (SPL-167): it
///   costs the full-round action.
/// - PHB p.174 (Casting Time): SpellData.ActionType gives the casting time; a 1-round casting time is a full-round
///   action (its effect still comes in the casting turn, SPL-162, SPL-164).
/// - PHB p.174 (Casting Time): a spell whose casting time is 1 free action (Feather Fall) may be cast only once per
///   round and provokes no attack of opportunity; PHB p.229 (Feather Fall) counts it toward the one quickened spell per
///   round. Explicit RAW.
/// - Items: a scroll or wand costs the item's own action today (CastActionCost.ItemOrDelivery), not the spell's
///   casting time, and a scroll of Feather Fall provokes (DMG p.213; ITM-086).
/// </summary>
public static class CastActionRules
{
    public const string RefusalQuickenSpontaneous = "quicken-spontaneous";
    public const string RefusalQuickenOncePerRound = "quicken-once-per-round";
    public const string RefusalNoFullRoundAction = "no-full-round-action";
    public const string RefusalNoSwiftAction = "no-swift-action";

    /// <summary>True when <paramref name="metamagic"/> quickens the cast (a held charge's delivery is not a casting, PHB p.141).</summary>
    public static bool IsQuickened(MetamagicData metamagic, CastSource source)
    {
        return metamagic != null && metamagic.Has(MetamagicFeatId.QuickenSpell)
               && (source == null || source.Kind != CastSourceKind.HeldChargeDelivery);
    }

    /// <summary>
    /// True when the cast is the caster's one quickened or free-action spell of the round and provokes no attack of
    /// opportunity: a cast whose casting action is a free action (<see cref="CastingAction"/>), that is a quickened cast
    /// (PHB p.98) or a spell whose casting time is a free action (Feather Fall, PHB p.174, p.229), unless spontaneous
    /// metamagic made it a full-round action (PHB p.88). Not a held charge's delivery (no casting, PHB p.141) nor a
    /// scroll or wand, whose activation costs the item's own action today and provokes as before (ITM-086).
    /// </summary>
    public static bool CountsAsQuickenedSpell(SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (source != null && (source.Kind == CastSourceKind.HeldChargeDelivery || source.IsItem))
            return false;
        return CastingAction(spell, metamagic, source) == CastActionCost.Free;
    }

    /// <summary>The action the cast charges (see <see cref="CastActionCost"/>).</summary>
    public static CastActionCost CastingAction(SpellData spell, MetamagicData metamagic, CastSource source)
    {
        CastSourceKind kind = source != null ? source.Kind : CastSourceKind.PreparedSlot;
        if (kind == CastSourceKind.HeldChargeDelivery || kind == CastSourceKind.Scroll || kind == CastSourceKind.Wand)
            return CastActionCost.ItemOrDelivery;
        if (IsQuickened(metamagic, source))
            return CastActionCost.Free;

        // PHB p.88: a metamagic spell cast spontaneously takes a full-round action, a free- or swift-action spell too
        // (delegated RAW reading 2026-10-14, see the class summary); for a 1-round spell an extra full round, which
        // costs the full round here (SPL-167).
        if (metamagic != null && metamagic.HasAnyMetamagic && source != null && source.IsSpontaneous)
            return CastActionCost.FullRound;

        SpellActionType dataAction = spell != null ? spell.ActionType : SpellActionType.Standard;
        if (dataAction == SpellActionType.Free)
            return CastActionCost.Free;
        if (dataAction == SpellActionType.Swift)
            return CastActionCost.Swift;
        return dataAction == SpellActionType.FullRound ? CastActionCost.FullRound : CastActionCost.Standard;
    }

    /// <summary>
    /// Why the cast may not be made as it stands, or null: Quicken Spell on a spontaneous cast (PHB p.98 Special), a
    /// second quickened or free-action spell this round (PHB p.98; PHB p.174, p.229 for Feather Fall), or a full-round
    /// or swift casting action the caster no longer has (PHB p.138, p.174). A caster limited to a single action
    /// (disabled, staggered) is refused a full-round casting outright: PHB p.142 (Start/Complete Full-Round Action)
    /// would let it start the casting this round and finish it next round, which the game cannot do yet (SPL-168). The F12 test panel's casts are not refused for a spent action (the
    /// panel casts out of turn); the quicken rules bind them too. A standard action is not tested here: the PC menu
    /// offers casting only with one, and the NPC path's own test refuses without one (LegacyCastQuirk.NpcOwnLegalityChecks;
    /// batch 11 step 22 moves it here).
    /// </summary>
    public static string Refusal(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (spell == null)
            return null;

        if (IsQuickened(metamagic, source) && source != null && source.IsSpontaneous)
            return RefusalQuickenSpontaneous;
        if (caster == null)
            return null;

        SpellcastingComponent spellComp = caster.Spellcasting;
        if (CountsAsQuickenedSpell(spell, metamagic, source) && spellComp != null && spellComp.HasCastQuickenedSpellThisRound)
            return RefusalQuickenOncePerRound;

        if (source != null && source.Kind == CastSourceKind.TestPanel)
            return null;

        ActionEconomy actions = caster.Actions;
        if (actions == null)
            return null;
        switch (CastingAction(spell, metamagic, source))
        {
            case CastActionCost.FullRound:
                return actions.HasFullRoundAction ? null : RefusalNoFullRoundAction;
            case CastActionCost.Swift:
                return actions.HasSwiftAction ? null : RefusalNoSwiftAction;
            default:
                return null;
        }
    }

    /// <summary>The player-facing text of a <see cref="Refusal"/> code.</summary>
    public static string DescribeRefusal(string refusal, string casterName, string spellName)
    {
        switch (refusal)
        {
            case RefusalQuickenSpontaneous:
                return $"{casterName} cannot quicken {spellName}: Quicken Spell cannot be applied to a spell cast spontaneously (PHB p.98).";
            case RefusalQuickenOncePerRound:
                return $"{casterName} has already cast a quickened spell this round and cannot cast {spellName} as one (PHB p.98).";
            case RefusalNoFullRoundAction:
                return $"{casterName} cannot cast {spellName}: casting it takes a full-round action (PHB p.174, p.88).";
            case RefusalNoSwiftAction:
                return $"{casterName} cannot cast {spellName}: no swift action left this round.";
            default:
                return $"{casterName} cannot cast {spellName} now.";
        }
    }
}
