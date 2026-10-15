using System;

/// <summary>How a decider answered the choices a cast still lacked (<see cref="ICastDecider.FillChoices"/>).</summary>
public enum CastChoiceFill
{
    /// <summary>Every choice the cast needs is made; the cast goes on.</summary>
    Filled,

    /// <summary>
    /// The decider is asking (a dialog is open). The run stops without an outcome and the cast stays open; the dialog
    /// re-enters the cast with the answer (the UI calls PerformSpellCast or BeginPendingSpellTargeting again) or ends it.
    /// </summary>
    Suspended,

    /// <summary>The choice cannot be made (no weapon to enchant): the cast is refused at Legality.</summary>
    Refused
}

/// <summary>How a melee touch spell is delivered (<see cref="ICastDecider.ChooseTouchDelivery"/>, PHB p.141 and p.176).</summary>
public enum CastTouchDelivery
{
    /// <summary>Cast now and touch a target this turn (the spell's own targeting).</summary>
    CastNow,

    /// <summary>Cast now and hold the charge, to deliver it later (SpellCastRequest.ForUiHoldCharge).</summary>
    HoldCharge,

    /// <summary>Back out before casting; nothing is spent.</summary>
    Cancel
}

/// <summary>The answer to a threatened cast (<see cref="ICastDecider.ChooseProvocation"/>), after its rolls.</summary>
public enum CastProvocationAnswer
{
    /// <summary>Cast defensively and passed, or cast normally and kept the spell through the attacks of opportunity.</summary>
    Proceeds,

    /// <summary>The spell is lost (a failed defensive cast, a failed Concentration check after an AoO, or the caster fell).</summary>
    Lost,

    /// <summary>The caster backed out before casting (the AoO prompt's Cancel); everything the cast spent is given back.</summary>
    Cancelled
}

/// <summary>
/// The only difference between a PC cast and an NPC cast (batch 11, cast_core_batch11.md; PC_NPC_PARITY): who makes the
/// caster's choices. The UI decider asks the player through the dialogs and the AoO prompt and can suspend the cast;
/// the AI decider answers inline. The cast core (GameManager.ExecuteCast) asks the decider at fixed points; the rules
/// stay in the core's stages. Still to come: ForgoSave and ChooseMirrorImageSwap (later steps).
/// </summary>
public interface ICastDecider
{
    /// <summary>True when every choice is answered inline (the AI), false when the cast may wait for the player.</summary>
    bool IsSynchronous { get; }

    /// <summary>
    /// Makes the caster's choices the cast still lacks (Legality stage, after the target test and before the component
    /// test). A decider that asks returns <see cref="CastChoiceFill.Suspended"/>; one that refuses logs why.
    /// </summary>
    CastChoiceFill FillChoices(GameManager gm, CastContext ctx);

    /// <summary>
    /// A melee touch spell was chosen (batch 11 step 10): cast it now, cast it and hold the charge, or back out. Calls
    /// <paramref name="answer"/> once, at once or after the player answered (the UI's touch prompt).
    /// </summary>
    void ChooseTouchDelivery(GameManager gm, CastContext ctx, Action<CastTouchDelivery> answer);

    /// <summary>
    /// A threatened cast that provokes (Provocation stage): choose to cast defensively, cast normally or cancel, resolve
    /// the rolls of that choice (GameManager.ResolveThreatenedSpellcast) and call <paramref name="answer"/> once,
    /// at once or after the player answered.
    /// </summary>
    void ChooseProvocation(GameManager gm, CastContext ctx, Action<CastProvocationAnswer> answer);

    /// <summary>
    /// A resolved cast is about to run its combat-end check (CombatEnd stage, after the result log went out): the
    /// decider's follow-up that must come before it, such as the UI's stat-panel refresh (which also refreshes the
    /// Flanked conditions) and highlight clearing, in the order the old PerformSpellCast ran them.
    /// </summary>
    void BeforeCombatEndCheck(GameManager gm, CastContext ctx);

    /// <summary>
    /// The cast has finished (GameManager.FinishCast, after its log and EndCast): the decider's own follow-up, such as
    /// the UI's return to the action menu or its pause before the next action. Stage code never does UI work.
    /// </summary>
    void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome);
}
