/// <summary>
/// The decider that answers for a creature (batch 11 step 18): the PC decider for a creature the player controls, the
/// AI decider for every other. Only a question put to a creature other than the caster needs it (a target giving up its
/// save, <see cref="ICastDecider.ForgoSave"/>); the caster's own decider is the request's (SpellCastRequest.Decider).
/// Choosing the decider is the only place that reads IsControllable (PC_NPC_PARITY).
/// </summary>
public static class CastDeciders
{
    public static ICastDecider For(CharacterController creature)
        => creature != null && creature.IsControllable ? (ICastDecider)UiCastDecider.Instance : AiCastDecider.Instance;
}

/// <summary>
/// The saving throw and spell resistance rules of a spell's effect on one creature (batch 11 step 18,
/// `raw-saves-and-sr`): the voluntary save (PHB p.177), the save with the spell-and-caster context (SaveRules, SPL-018)
/// and the one spell resistance check (SpellSaveResolver.RollSpellResistance with the cast's caster level and Spell
/// Penetration, SPL-122). Every caster and target alike (PC_NPC_PARITY). CastContext.TargetForgoesSave, RollSave and
/// RollSpellResistance are these for the cast's caster and spell; GameManager.RollSpellSave reaches them from a spell
/// handler that has no context in hand.
/// </summary>
public static class SpellSaveRules
{
    /// <summary>
    /// Whether <paramref name="target"/> gives up its save against <paramref name="spell"/> cast by
    /// <paramref name="caster"/>: its own decider answers (<see cref="CastDeciders.For"/>, ICastDecider.ForgoSave).
    /// PHB p.177: a creature may choose to give up its save and take the spell's effect; nothing makes it do so.
    /// </summary>
    public static bool TargetForgoesSave(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (caster == null || target == null || spell == null)
            return false;
        return CastDeciders.For(target).ForgoSave(target, caster, spell);
    }

    /// <summary>
    /// The default both deciders apply (batch 11 step 18; delegated RAW reading 2026-10-14): a creature gives up its save
    /// only against a spell of its own side (the caster itself or an ally) that is harmless or beneficial to it, judged
    /// by the spell's runtime <see cref="SpellData.EffectType"/> (the database's, after
    /// SpellCategoryClassifier.ReclassifyAll). It never gives up its save against a Damage, Debuff, Control or Wall spell
    /// (an effect that hurts or hinders whoever it reaches), nor against a Healing spell when it is not a living creature
    /// (healing harms the undead, PHB p.215), nor against a foe's spell. Every other type is the help the PHB marks
    /// "(harmless)": a Buff, Healing on the living, and the Dispel, Illusion, Utility, Escape and Divination spells (Remove
    /// Curse, PHB p.271; Invisibility and Displacement, PHB p.245, p.223). A charmed or confused creature keeps only the
    /// Buff case: behaviour kept from before step 18 by the batch 11 plan, not a reading of rules text (PHB p.177 puts no
    /// condition on giving up a save). It stands because such a creature does not choose freely (a confused one acts by
    /// its roll, PHB p.212), so it is not trusted to take more than plain help; the owner may overrule it (RULES_COVERAGE
    /// 4.1). A creature charmed by a caster of the other side still rolls against that charmer's Buff, though it regards
    /// the charmer as a friend (PHB p.209): only the caster's own side counts.
    /// </summary>
    public static bool IsHarmlessOrBeneficialTo(CharacterController self, CharacterController caster, SpellData spell)
    {
        if (self == null || caster == null || spell == null)
            return false;
        bool ownSide = self == caster || TeamUtility.IsAlly(caster, self);
        if (!ownSide)
            return false;
        if (spell.EffectType == SpellEffectType.Buff)
            return true;
        bool compelled = self.HasCondition(CombatConditionType.Charmed) || self.HasCondition(CombatConditionType.Confused);
        if (compelled)
            return false;
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
            case SpellEffectType.Debuff:
            case SpellEffectType.Control:
            case SpellEffectType.Wall:
                return false;
            case SpellEffectType.Healing:
                return SpellCaster.IsLivingCreature(self.Stats);
            default:
                return true;
        }
    }

    /// <summary>A save the target gave up: no die, a failed save (as SpellCaster.Cast records one: roll 1, total 1).</summary>
    public static SaveResult ForgoneSave(SaveType save, int dc)
        => new SaveResult { Type = save, Roll = 1, Modifier = 0, Total = 1, DC = dc, Saved = false, Forgone = true };

    /// <summary>
    /// <paramref name="target"/>'s save against <paramref name="spell"/> cast by <paramref name="caster"/>: given up when
    /// <see cref="TargetForgoesSave"/>, else one d20 (under <paramref name="diceContext"/>) with SaveRules.Modifier for the
    /// spell-and-caster context (SaveContext.ForSpellCast: the target's own situational bonuses and the spell's, SPL-018)
    /// plus <paramref name="extraModifier"/>, and a countersong (SpellSaveResolver.RollSave).
    /// </summary>
    public static SaveResult RollSave(CharacterController caster, CharacterController target, SpellData spell, SaveType save,
        int dc, string diceContext = null, int extraModifier = 0)
    {
        if (TargetForgoesSave(caster, target, spell))
            return ForgoneSave(save, dc);
        return SpellSaveResolver.RollSave(target, save, dc, SaveContext.ForSpellCast(spell, caster), diceContext, extraModifier);
    }

    /// <summary>
    /// The caster level check against <paramref name="target"/>'s spell resistance (PHB p.177): d20 + the cast's caster
    /// level (<paramref name="casterLevel"/>, or CasterLevelRules.ForCaster when below 1) + Spell Penetration (PHB p.100).
    /// Skipped, with no roll, when the target has no SR. The caller decides whether the spell allows SR at all.
    /// </summary>
    public static SRResult RollSpellResistance(CharacterController caster, CharacterController target, SpellData spell,
        string diceContext = null, int casterLevel = 0)
    {
        if (casterLevel < 1)
            casterLevel = caster != null && caster.Stats != null ? CasterLevelRules.ForCaster(caster, spell) : 0;
        return SpellSaveResolver.RollSpellResistance(caster, target, casterLevel, diceContext);
    }
}
