using System;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// GameManager partial class: the deciders' side of the cast core (batch 11 step 7, docs/designs/cast_core_batch11.md).
///
/// <see cref="UiCastDecider"/> calls the Ui methods here: the PC's choice dialogs, the AoO prompt and the UI follow-up of
/// a finished cast. This is the only cast-core code that touches CombatUI beyond the log, the sub-phase, the highlights,
/// the stat panels, ShowActionChoices and AfterAttackDelay; the stages themselves (GameManager.CastCore.cs) never do.
/// <see cref="AiCastDecider"/> calls <see cref="AiResolveCastProvocation"/> and <see cref="AiOnCastFinished"/> (the NPC
/// path runs through the core since step 8: TryNPCPerformSpellCast, RunCastSync).
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The PC's choices for the cast: the Resist Energy, Protection from Energy and Fire Shield dialogs (which re-enter
    /// the cast through BeginPendingSpellTargeting once answered), then the Magic Weapon, Keen Edge and Greater Magic
    /// Weapon pickers (which re-enter through PerformSpellCast). A picker with nothing to pick refuses the cast.
    /// </summary>
    internal CastChoiceFill UiFillCastChoices(CastContext ctx)
    {
        SpellCastRequest req = ctx.Request;
        CharacterController caster = req.Caster;
        CharacterController target = req.Aim.Target;
        string spellId = req.Spell.SpellId;

        if (string.Equals(spellId, SpellNames.RESIST_ENERGY, StringComparison.Ordinal) && !req.Choices.Energy.HasValue)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Resist Energy requires selecting an energy type before casting."));
            ShowResistEnergyTypeSelection(caster);
            return CastChoiceFill.Suspended;
        }

        if (string.Equals(spellId, SpellNames.PROTECTION_FROM_ENERGY, StringComparison.Ordinal) && !req.Choices.Energy.HasValue)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Protection from Energy requires selecting an energy type before casting."));
            ShowProtectionFromEnergyTypeSelection(caster);
            return CastChoiceFill.Suspended;
        }

        if (string.Equals(spellId, SpellNames.FIRE_SHIELD, StringComparison.Ordinal) && !req.Choices.FireShieldWarm.HasValue)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Fire Shield requires selecting a shield type before casting."));
            ShowFireShieldTypeSelection(caster);
            return CastChoiceFill.Suspended;
        }

        CastChoiceFill fill = TryHandleMagicWeaponWeaponSelection(caster, target);
        if (fill != CastChoiceFill.Filled)
            return fill;

        fill = TryHandleKeenEdgeWeaponSelection(caster, target);
        if (fill != CastChoiceFill.Filled)
            return fill;

        return TryHandleGreaterMagicWeaponSelection(caster, target);
    }

    /// <summary>
    /// The PC's choice for a melee touch spell (batch 11 step 10; PHB p.141, p.176): the touch prompt's Cast Now,
    /// Discharge Later and Cancel buttons (CombatUI.ShowTouchSpellPrompt). Without a CombatUI nothing is asked and the
    /// cast stays open, as before.
    /// </summary>
    internal void UiChooseTouchDelivery(CastContext ctx, Action<CastTouchDelivery> answer)
    {
        CombatUI?.ShowTouchSpellPrompt(
            ctx.Request.Spell,
            onCastNow: () => answer(CastTouchDelivery.CastNow),
            onDischargeLater: () => answer(CastTouchDelivery.HoldCharge),
            onCancel: () => answer(CastTouchDelivery.Cancel));
    }

    /// <summary>
    /// The PC's answer to a threatened cast: the AoO prompt (ShowAoOActionConfirmation, through
    /// ResolveSpellcastProvocation), whose Cast Defensively and Proceed buttons resolve the rolls through
    /// ResolveThreatenedSpellcast and whose Cancel backs out.
    /// </summary>
    internal void UiChooseCastProvocation(CastContext ctx, Action<CastProvocationAnswer> answer)
    {
        ResolveSpellcastProvocation(ctx.Request.Caster, ctx.Request.Spell, false, canProceed =>
            answer(canProceed
                ? CastProvocationAnswer.Proceeds
                : _spellcastProvocationCancelled ? CastProvocationAnswer.Cancelled : CastProvocationAnswer.Lost));
    }

    /// <summary>
    /// The UI follow-up of a resolved PC cast that comes before its combat-end check, in the old PerformSpellCast order:
    /// the result log becomes the last combat log, the stat panels refresh (UpdateAllStatsUI, which also applies and
    /// removes the Flanked conditions) and the highlights clear.
    /// </summary>
    internal void UiBeforeCastCombatEndCheck(CastContext ctx)
    {
        if (ctx.ResultLog != null)
            _lastCombatLog = ctx.ResultLog;
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();
    }

    /// <summary>
    /// The UI follow-up of a finished PC cast: a refusal, a cancel, a failed spend or an outcome that asks for it
    /// (<see cref="CastOutcome.ShowsActionMenu"/>: a summon that spawned nothing, a summon or Grease lost before its
    /// provocation) returns to the action menu; any
    /// other cast refreshes the stat panels, clears the highlights and keeps its result log as the last combat log (a
    /// cast resolved at the CombatEnd stage did that before its combat-end check, UiBeforeCastCombatEndCheck) and,
    /// unless it ended the combat, pauses before the next action (AfterAttackDelay: 1.5 s after a resolved cast, 1 s
    /// after a lost, failed or mirrored one).
    /// </summary>
    internal void UiOnCastFinished(CastContext ctx, CastOutcome outcome)
    {
        CharacterController caster = ctx.Request.Caster;

        if (outcome.Kind == CastOutcomeKind.Cancelled)
        {
            Grid.ClearAllHighlights();
            UpdateAllStatsUI();
            ShowActionChoices();
            return;
        }

        if (outcome.BackToActionMenu || outcome.ShowsActionMenu)
        {
            ShowActionChoices();
            return;
        }

        bool resolvedAtCombatEnd = outcome.Kind == CastOutcomeKind.Resolved && outcome.At == CastStage.CombatEnd;
        if (!resolvedAtCombatEnd)
        {
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();
            if (ctx.ResultLog != null)
                _lastCombatLog = ctx.ResultLog;
        }

        if (outcome.CombatEnded)
            return;

        float delay = resolvedAtCombatEnd ? 1.5f : 1.0f;
        if (caster != null)
            StartCoroutine(AfterAttackDelay(caster, delay));
        else
            ShowActionChoices();
    }

    /// <summary>The NPC decision and rolls for a threatened cast (AiCastDecider.ChooseProvocation).</summary>
    internal bool AiResolveCastProvocation(CastContext ctx)
    {
        return ResolveNPCSpellcastProvocation(ctx.Request.Caster, ctx.Request.Spell);
    }

    /// <summary>
    /// The follow-up of a finished NPC cast (AiCastDecider.OnCastFinished), as the old NPC executor did it before it
    /// returned true: a cast that counts as cast (<see cref="CastOutcome.WasCast"/>) keeps its result log, if it has one
    /// (a resolved cast or a struck mirror image), as the last combat log and refreshes the stat panels, which also
    /// applies and removes the Flanked conditions. A refused or unspendable cast (the old false returns) does neither.
    /// The combat-end check comes after, in TryNPCPerformSpellCastForAI (LegacyCastQuirk.NpcCombatEndCheckedByCaller).
    /// </summary>
    internal void AiOnCastFinished(CastContext ctx, CastOutcome outcome)
    {
        if (outcome == null || !outcome.WasCast)
            return;
        if (ctx.ResultLog != null)
            _lastCombatLog = ctx.ResultLog;
        UpdateAllStatsUI();
    }
}
