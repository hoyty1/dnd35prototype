/// <summary>The action a cast charges (<see cref="CastActionRules.CastingAction"/>).</summary>
public enum CastActionCost
{
    /// <summary>
    /// Nothing at the cast: a held-charge delivery is no casting; its cost is <see cref="HeldChargeDeliveryRules"/>'s
    /// (batch 12 step 18, SPL-156). A scroll or wand cast costs its own action since batch 13 step 14 (D7; see
    /// <see cref="CastActionRules.CastingAction"/>).
    /// </summary>
    Delivery,

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
/// alike (docs/systems/PC_NPC_PARITY.md). Pure: no side effects. Since batch 11 step 21 the metamagic rules live in
/// MetamagicSystem (Spell/Components/MetamagicModifier.cs, SPL-055): <see cref="CastingAction"/> asks
/// MetamagicSystem.GetMetamagicCastingAction for every source but an item or a delivery, and <see cref="Refusal"/> asks
/// MetamagicSystem.ValidateMetamagicApplication before its own action tests (<see cref="ActionRefusal"/>).
///
/// RAW applied:
/// - PHB p.98 (Quicken Spell): a quickened spell is a free action and does not provoke; one quickened spell per round;
///   the feat cannot be applied to a spell cast spontaneously (MetamagicSystem).
/// - PHB p.88 (Casting Time; Spontaneous Casting and Metamagic Feats): a spontaneous cast with a metamagic feat takes a
///   full-round action for a 1-action spell, a free- or swift-action spell included; the source of the cast decides, not
///   the caster's classes (delegated RAW readings 2026-10-14; MetamagicSystem.GetMetamagicCastingAction holds the rule).
///   A spell with a longer casting time takes an extra full-round action, which the game cannot express (SPL-167): it
///   costs the full-round action.
/// - PHB p.174 (Casting Time): SpellData.ActionType gives the casting time; a 1-round casting time is a full-round
///   action (its effect still comes in the casting turn, SPL-162, SPL-164).
/// - PHB p.174 (Casting Time): a spell whose casting time is 1 free action (Feather Fall) may be cast only once per
///   round and provokes no attack of opportunity; PHB p.229 (Feather Fall) counts it toward the one quickened spell per
///   round. Explicit RAW.
/// - Items (batch 13 step 14, D7): DMG p.213 (Activation): the casting time of a spell is the time needed to activate the
///   same power in an item of any kind (scroll, wand or other). A scroll (spell completion) or a wand
///   (spell trigger) takes the spell's casting time, at least a standard action (a full round for a 1-round spell); a
///   scroll provokes as casting does, a wand does not (DMG p.213, p.245). Delegated RAW readings 2026-10-14 (a scroll),
///   extended to a wand 2026-10-29 (RULES_COVERAGE 4.1, D7): the same sentence covers both item kinds, so a scroll or wand of a free-action spell
///   (Feather Fall) or a quickened one is a free action that provokes nothing and counts as the round's free-action
///   spell; a swift-action spell's scroll or wand stays a standard action (the items' own "standard action"). Retrieving
///   the item first is the use-item action's own move action (GameManager.ActionCore_UseItem).
/// </summary>
public static class CastActionRules
{
    public const string RefusalQuickenSpontaneous = MetamagicSystem.RefusalQuickenSpontaneous;
    public const string RefusalQuickenOncePerRound = "quicken-once-per-round";
    public const string RefusalNoFullRoundAction = "no-full-round-action";
    public const string RefusalNoSwiftAction = "no-swift-action";
    public const string RefusalNoFurtherActions = "no-further-actions";
    public const string RefusalNoStandardAction = "no-standard-action";

    /// <summary>True when <paramref name="metamagic"/> quickens the cast (MetamagicSystem.IsQuickened; a held charge's delivery is not a casting, PHB p.141).</summary>
    public static bool IsQuickened(MetamagicData metamagic, CastSource source)
    {
        return MetamagicSystem.IsQuickened(metamagic, source);
    }

    /// <summary>
    /// True when the cast is the caster's one quickened or free-action spell of the round and provokes no attack of
    /// opportunity: a cast whose casting action is a free action (<see cref="CastingAction"/>), that is a quickened cast
    /// (PHB p.98) or a spell whose casting time is a free action (Feather Fall, PHB p.174, p.229), unless spontaneous
    /// metamagic made it a full-round action (PHB p.88). A scroll or wand of such a spell counts too (DMG p.213: the item
    /// takes the spell's casting time; batch 13 step 14, D7, delegated RAW reading 2026-10-29). Not a held charge's
    /// delivery (no casting, PHB p.141).
    /// </summary>
    public static bool CountsAsQuickenedSpell(SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (source != null && source.Kind == CastSourceKind.HeldChargeDelivery)
            return false;
        return CastingAction(spell, metamagic, source) == CastActionCost.Free;
    }

    /// <summary>
    /// The action the cast charges (see <see cref="CastActionCost"/>): none for a held charge's delivery, else the
    /// casting action with the cast's metamagic (MetamagicSystem.GetMetamagicCastingAction, batch 11 step 21), which an
    /// item adjusts (batch 13 step 14, D7; DMG p.213): a scroll or wand takes the casting time but at least a standard
    /// action (a swift-action spell's item is a standard action; a free-action or quickened spell's stays free; a 1-round
    /// spell's is a full round).
    /// </summary>
    public static CastActionCost CastingAction(SpellData spell, MetamagicData metamagic, CastSource source)
    {
        CastSourceKind kind = source != null ? source.Kind : CastSourceKind.PreparedSlot;
        if (kind == CastSourceKind.HeldChargeDelivery)
            return CastActionCost.Delivery;
        CastActionCost casting = MetamagicSystem.GetMetamagicCastingAction(spell, metamagic, source);
        if (kind == CastSourceKind.Scroll || kind == CastSourceKind.Wand)
            return casting == CastActionCost.Swift ? CastActionCost.Standard : casting;
        return casting;
    }

    /// <summary>
    /// Why the cast may not be made as it stands, or null: first the metamagic rules
    /// (MetamagicSystem.ValidateMetamagicApplication, batch 11 step 21: Quicken Spell on a spontaneous cast, PHB p.98
    /// Special; a feat that cannot modify the spell, PHB p.88; a feat the caster lacks; a Heighten that raises nothing;
    /// an adjusted level above 9th), then the casting action (<see cref="ActionRefusal"/>). The PC spell menu and the AI's spell choice
    /// ask this; the cast core's Legality stage asks the two in turn (GameManager.CheckCastMetamagicLegality,
    /// CheckCastActionLegality).
    /// </summary>
    public static string Refusal(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        string metamagicRefusal = MetamagicSystem.ValidateMetamagicApplication(spell, metamagic, caster != null ? caster.Stats : null, source);
        return metamagicRefusal ?? ActionRefusal(caster, spell, metamagic, source);
    }

    /// <summary>
    /// Why the casting action cannot be taken now, or null: a second quickened or free-action spell this round (PHB p.98;
    /// PHB p.174, p.229 for Feather Fall), or a full-round or swift casting action the caster no longer has (PHB p.138,
    /// p.174). A caster limited to a single action (disabled, staggered) is refused a full-round casting outright: PHB
    /// p.142 (Start/Complete Full-Round Action) would let it start the casting this round and finish it next round, which
    /// the game cannot do yet (SPL-168). The F12 test panel's casts are not refused for a spent action (the panel casts
    /// out of turn); the quicken rules bind them too. A standard action is not tested here but in
    /// <see cref="StandardActionRefusal"/>, which the cast core's caster tests run for every caster (batch 12 step 9,
    /// SPL-160) and the AI's planning leaves out (it sizes an approach before the caster has acted).
    /// </summary>
    public static string ActionRefusal(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (spell == null || caster == null)
            return null;

        SpellcastingComponent spellComp = caster.Spellcasting;
        if (CountsAsQuickenedSpell(spell, metamagic, source) && spellComp != null && spellComp.HasCastQuickenedSpellThisRound)
            return RefusalQuickenOncePerRound;

        if (source != null && source.Kind == CastSourceKind.TestPanel)
            return null;

        ActionEconomy actions = caster.Actions;
        if (actions == null)
            return null;
        // A disabled caster whose strenuous act will leave it dying takes nothing further this turn, a quickened spell
        // included (ActionEconomy.NoFurtherActions, PHB p.145).
        if (actions.NoFurtherActions && CountsAsQuickenedSpell(spell, metamagic, source))
            return RefusalNoFurtherActions;
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

    /// <summary>
    /// Why the caster lacks the action its casting needs, or null (batch 12 step 9, SPL-160 a; one test for every caster,
    /// formerly the NPC path's own): a casting that costs a standard action or a full round, a scroll's or wand's
    /// included (<see cref="CastingAction"/>, DMG p.213), needs a standard action left this round (PHB p.140-141); a
    /// quickened or free-action spell (a scroll of one too), or a swift one, does not (PHB p.98, p.174), and <see cref="ActionRefusal"/> holds it
    /// to its own action. A held charge's delivery is no casting (PHB p.141): its own action test is
    /// <see cref="HeldChargeDeliveryRules.Refusal"/> (batch 12 step 18, SPL-156); the F12 test panel casts out of turn, so
    /// neither is tested here.
    /// </summary>
    public static string StandardActionRefusal(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (caster == null || spell == null)
            return null;
        if (source != null && (source.Kind == CastSourceKind.TestPanel || source.Kind == CastSourceKind.HeldChargeDelivery))
            return null;
        CastActionCost cost = CastingAction(spell, metamagic, source);
        if (cost == CastActionCost.Free || cost == CastActionCost.Swift)
            return null;
        return caster.Actions != null && caster.Actions.HasStandardAction ? null : RefusalNoStandardAction;
    }

    /// <summary>The player-facing text of a <see cref="Refusal"/> code (a metamagic one through MetamagicSystem.DescribeRefusal).</summary>
    public static string DescribeRefusal(string refusal, string casterName, string spellName, MetamagicData metamagic = null)
    {
        string metamagicText = MetamagicSystem.DescribeRefusal(refusal, casterName, spellName, metamagic);
        if (metamagicText != null)
            return metamagicText;
        switch (refusal)
        {
            case RefusalQuickenOncePerRound:
                return $"{casterName} has already cast a quickened spell this round and cannot cast {spellName} as one (PHB p.98).";
            case RefusalNoFullRoundAction:
                return $"{casterName} cannot cast {spellName}: casting it takes a full-round action (PHB p.174, p.88).";
            case RefusalNoSwiftAction:
                return $"{casterName} cannot cast {spellName}: no swift action left this round.";
            case RefusalNoFurtherActions:
                return $"{casterName} cannot cast {spellName}: acting while disabled has spent this turn (PHB p.145).";
            case RefusalNoStandardAction:
                return $"{casterName} cannot cast {spellName}: no standard action left this round (PHB p.140-141).";
            default:
                return $"{casterName} cannot cast {spellName} now.";
        }
    }
}

/// <summary>What delivering a held touch charge costs (<see cref="HeldChargeDeliveryRules.CostFor"/>).</summary>
public enum HeldChargeDeliveryCost
{
    /// <summary>The touch that comes with the casting, in its casting round (PHB p.140-141).</summary>
    None,
    /// <summary>A melee touch attack on a foe, made in place of a regular attack: the attack action or one step of a full attack (PHB p.141).</summary>
    AttackStep,
    /// <summary>Touching one friend (or the caster itself): a standard action (PHB p.141, Holding the Charge).</summary>
    StandardAction
}

/// <summary>
/// The action cost of delivering a held touch charge (batch 12 step 18, SPL-156), one rule for every caster, PC or NPC
/// (the cast core's Legality and Cost stages, the PC Discharge button's gate and the AI's delivery all read it). PHB
/// p.140-141 (Touch Spells in Combat): in the round the spell is cast the caster may touch as part of the casting, moving
/// first if it likes; holding the charge, it may make touch attacks round after round (PHB p.141, p.176), each a touch
/// attack made instead of a regular attack. Delegated RAW reading 2026-10-14 (docs/systems/RULES_COVERAGE.md 4.1): after
/// the casting's own touch (in its casting round, unused: SpellcastingComponent.IsCastingRoundTouchAvailable), touching a
/// foe takes an attack, the attack action (a standard action) or one attack of a full attack (CharacterController
/// .TryCommitAttack; run through the attack-sequence executor, AttackSequenceRequest.ForHeldChargeTouch); touching one
/// friend, or the caster itself, is a standard action (PHB p.141). Touching up to six friends as a full-round action
/// (PHB p.141) is not offered: a delivery touches one creature.
/// </summary>
public static class HeldChargeDeliveryRules
{
    /// <summary>The Legality refusal of a delivery to a foe with no attack left this turn.</summary>
    public const string RefusalNoAttack = "no-attack-for-delivery";

    /// <summary>The cost of delivering <paramref name="caster"/>'s held charge to <paramref name="target"/> now.</summary>
    public static HeldChargeDeliveryCost CostFor(CharacterController caster, CharacterController target)
    {
        SpellcastingComponent spellComp = caster != null ? caster.Spellcasting : null;
        if (spellComp != null && spellComp.IsCastingRoundTouchAvailable(caster))
            return HeldChargeDeliveryCost.None;
        if (target == null || target == caster || TeamUtility.IsAlly(caster, target))
            return HeldChargeDeliveryCost.StandardAction;
        return HeldChargeDeliveryCost.AttackStep;
    }

    /// <summary>
    /// Why <paramref name="caster"/> cannot pay for delivering its charge to <paramref name="target"/> now, or null:
    /// a foe needs an attack it may still commit (CharacterController.CanCommitAttack: the attack ladder and the actions;
    /// <paramref name="stepCommitted"/> when the attack-sequence executor paid it already), a friend a standard action.
    /// </summary>
    public static string Refusal(CharacterController caster, CharacterController target, bool stepCommitted)
    {
        if (caster == null)
            return null;
        switch (CostFor(caster, target))
        {
            case HeldChargeDeliveryCost.AttackStep:
                return stepCommitted || caster.CanCommitAttack(AttackStepKind.MainHand, out _) ? null : RefusalNoAttack;
            case HeldChargeDeliveryCost.StandardAction:
                return caster.Actions != null && caster.Actions.HasStandardAction ? null : CastActionRules.RefusalNoStandardAction;
            default:
                return null;
        }
    }

    /// <summary>
    /// True when <paramref name="caster"/> could deliver its held charge to some creature now (the PC Discharge button's
    /// gate): the casting-round touch, an attack left for a foe, or a standard action for a friend.
    /// </summary>
    public static bool CanDeliverNow(CharacterController caster)
    {
        SpellcastingComponent spellComp = caster != null ? caster.Spellcasting : null;
        if (spellComp == null || !spellComp.HasHeldTouchCharge)
            return false;
        if (spellComp.IsCastingRoundTouchAvailable(caster))
            return true;
        return caster.CanCommitAttack(AttackStepKind.MainHand, out _)
               || (caster.Actions != null && caster.Actions.HasStandardAction);
    }
}
