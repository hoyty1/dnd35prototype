using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager partial class: the action catalog's runners (batch 13 step 3, docs/architecture/combat-actions.md).
/// - <see cref="RunPlayerAction"/>: the PC panel's way into a catalog executor; it starts the executor, then does the
///   entry's UI follow-up (<see cref="ActionDefinition.UiAfter"/>). A refusal shows its reason and the action panel.
/// - <see cref="RunAIAction"/>: the AI's, the harness's and a forced turn's (ActorSource.AI, C7), yielded as a nested
///   enumerator. It pre-checks <see cref="IActionExecutor.CanExecute"/> only for Extracted and New entries, where a
///   refusal spends nothing; a Delegating entry's core owns its refusals. A pick its actor cannot start is logged with
///   Debug.Log("[ActionSet] ...") (the shadow hook, which step 6 points at the legal set; never a combat-log line).
/// - <see cref="ResolveActionProvocation"/>: the one attack-of-opportunity loop for non-movement actions; it never ends a
///   turn. <see cref="ConfirmActionProvocation"/> is its prompt alone, for a core that takes its own AoOs (stand up).
/// They are internal (Assembly-CSharp: the tests, the harness, the AI). Total defense (batch 13 step 4) is their first
/// live caller: the PC's Total Defense button through RunPlayerAction, the cornered panicked creature
/// (FrightenedBehaviorController.ExecuteDecision) through RunAIAction; step 6 routes the rest of the AI through RunAIAction.
/// </summary>
public partial class GameManager
{
    /// <summary>Takes <paramref name="ctx"/>'s action for the player and continues the PC UI after it. Returns the running coroutine.</summary>
    internal Coroutine RunPlayerAction(ActionContext ctx, ActionOutcome outcome = null)
    {
        if (outcome == null)
            outcome = new ActionOutcome();
        if (ctx != null)
            ctx.Source = ActorSource.Ui;
        return StartCoroutine(RunPlayerActionRoutine(ctx, outcome));
    }

    private IEnumerator RunPlayerActionRoutine(ActionContext ctx, ActionOutcome o)
    {
        CharacterController actor = ctx != null ? ctx.Actor : null;
        Legality refusal = CheckRunnable(ctx);
        if (refusal.Ok && IsPreChecked(ctx.Definition))
            refusal = ctx.Definition.Executor.CanExecute(ctx);
        if (!refusal.Ok)
        {
            o.Refuse(refusal);
            ShowActionRefusal(actor, ctx, refusal);
            ShowActionChoices();
            yield break;
        }

        ctx.Decider = ActionDeciders.For(actor, ActorSource.Ui);
        ActionCosts.Snapshot before = ActionCosts.Take(actor);
        yield return ctx.Definition.Executor.Execute(ctx, o);
        FinishActionOutcome(ctx, o, before);

        // An entry with UiAfter None, or a core that continues the PC UI itself (the UI cast decider's finish
        // continuation), owns every follow-up: its refusal, its cancel and the end of the turn when the actor drops. The
        // runner adds nothing, so no line is logged twice and no turn ends twice (CORE-012).
        ActionDefinition def = ctx.Definition;
        if (def.UiAfter == ActionUiAfter.None || o.CoreOwnsUi)
            yield break;

        if (o.CombatEnded)
            yield break;
        if (o.ActorDown)
        {
            EndActivePCTurn();
            yield break;
        }
        if (!o.Performed)
        {
            if (!o.Cancelled && !o.RefusalLogged)
                ShowActionRefusal(actor, ctx, o.Refusal);
            ShowActionChoices();
            yield break;
        }

        switch (def.UiAfter)
        {
            case ActionUiAfter.ShowChoices:
                ShowActionChoices();
                break;
            case ActionUiAfter.AttackDelay:
                yield return AfterAttackDelay(actor, def.UiAfterSeconds);
                break;
            case ActionUiAfter.EndTurn:
                if (def.UiAfterSeconds > 0f)
                    yield return DelayedEndActivePCTurn(def.UiAfterSeconds);
                else
                    EndActivePCTurn();
                break;
        }
    }

    /// <summary>
    /// Takes <paramref name="ctx"/>'s action with the AI's choices (an NPC, a summon, the harness, a forced PC turn). The
    /// caller yields it and reads <paramref name="o"/>; nothing here continues the PC UI.
    /// </summary>
    internal IEnumerator RunAIAction(ActionContext ctx, ActionOutcome o)
    {
        if (o == null)
            o = new ActionOutcome();
        if (ctx != null)
            ctx.Source = ActorSource.AI;

        Legality refusal = CheckRunnable(ctx);
        if (!refusal.Ok)
        {
            o.Refuse(refusal);
            Debug.Log($"[ActionRunner] {DescribeAction(ctx)} refused before it ran: {refusal.Reason}");
            yield break;
        }

        ActionShadowCheck(ctx);

        if (IsPreChecked(ctx.Definition))
        {
            refusal = ctx.Definition.Executor.CanExecute(ctx);
            if (!refusal.Ok)
            {
                o.Refuse(refusal);
                Debug.Log($"[ActionRunner] {DescribeAction(ctx)} refused: {refusal.Reason} ({refusal.Bar})");
                yield break;
            }
        }

        ctx.Decider = ActionDeciders.For(ctx.Actor, ActorSource.AI);
        ActionCosts.Snapshot before = ActionCosts.Take(ctx.Actor);
        yield return ctx.Definition.Executor.Execute(ctx, o);
        FinishActionOutcome(ctx, o, before);
    }

    /// <summary>
    /// The attacks of opportunity a non-movement action provokes from the foes that threaten <paramref name="actor"/>
    /// (PHB p.137, Table 8-2): <paramref name="decider"/> confirms the action first (the UI's prompt; the AI always goes
    /// on), then each foe that can still make an AoO takes it, until the actor drops. A cancel refuses the action
    /// (<see cref="ActionOutcome.Cancelled"/>), with nothing spent here. It never ends a turn: the caller reads
    /// <see cref="ActionOutcome.ActorDown"/> and <see cref="ActionOutcome.CombatEnded"/>.
    /// </summary>
    internal IEnumerator ResolveActionProvocation(CharacterController actor, AoOProvokingAction action, IActionDecider decider, ActionOutcome outcome,
        string actionName = null)
    {
        if (outcome == null)
            outcome = new ActionOutcome();
        if (actor == null || actor.Stats == null)
            yield break;

        List<CharacterController> provokers = ThreatSystem.GetActionAoOProvokers(actor, GetAllCharacters());
        if (provokers.Count == 0)
            yield break;

        yield return ConfirmActionProvocation(actor, action, actionName, provokers, decider, outcome);
        if (outcome.Cancelled || outcome.CombatEnded || !outcome.Refusal.Ok)
            yield break;

        string label = string.IsNullOrEmpty(actionName) ? action.ToString() : actionName;
        foreach (CharacterController enemy in provokers)
        {
            if (CombatEndRules.IsOutOfFight(actor) || CurrentPhase == TurnPhase.CombatOver)
                break;
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoOAgainst(enemy, actor))
                continue;

            CombatResult aoo = ThreatSystem.ExecuteAoO(enemy, actor, trigger: label);
            if (aoo == null)
                continue;
            outcome.Provoked = true;
            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"AoO vs {label}: {aoo.GetDetailedSummary()}"));
            UpdateAllStatsUI();
            yield return new WaitForSeconds(0.65f);
        }

        outcome.ActorDown = CombatEndRules.IsOutOfFight(actor);
        outcome.CombatEnded = CurrentPhase == TurnPhase.CombatOver;
    }

    /// <summary>
    /// Asks <paramref name="decider"/> whether <paramref name="actor"/> goes on with an action that provokes from
    /// <paramref name="threateners"/> (the UI's prompt; the AI always goes on), and waits for the answer. Proceed leaves
    /// <paramref name="outcome"/> as it was; a cancel sets <see cref="ActionOutcome.Cancelled"/> and refuses; a prompt
    /// that could not be shown (one already open, a cast) refuses without a cancel; a prompt torn down without an answer
    /// (ShowActionChoices, CancelMovementSelection and the encounter reset clear it without calling OnCancel) counts as
    /// a cancel; a combat end sets <see cref="ActionOutcome.CombatEnded"/>. Nothing is spent here.
    /// </summary>
    internal IEnumerator ConfirmActionProvocation(CharacterController actor, AoOProvokingAction action, string actionName,
        List<CharacterController> threateners, IActionDecider decider, ActionOutcome outcome)
    {
        if (outcome == null)
            outcome = new ActionOutcome();
        if (decider == null)
            decider = ActionDeciders.For(actor);

        ProvocationAnswer? answer = null;
        decider.ConfirmProvocation(this, actor, action, actionName, threateners, a => answer = a);
        // An answer not given at once means the prompt is open (_waitingForAoOConfirmation); its callbacks clear that
        // flag just before they answer, so a cleared flag with no answer is a prompt torn down without one.
        while (answer == null && CurrentPhase != TurnPhase.CombatOver)
        {
            if (!_waitingForAoOConfirmation)
            {
                answer = ProvocationAnswer.Cancel;
                break;
            }
            yield return null;
        }

        if (CurrentPhase == TurnPhase.CombatOver)
        {
            outcome.CombatEnded = true;
            yield break;
        }
        switch (answer)
        {
            case ProvocationAnswer.Proceed:
                yield break;
            case ProvocationAnswer.Unavailable:
                outcome.Refuse(Legality.Refused(LegalityBar.Other, "no attack-of-opportunity prompt can be shown now"));
                yield break;
            default:
                outcome.Cancelled = true;
                outcome.Refuse(Legality.Refused(LegalityBar.Other, "cancelled at the attack-of-opportunity prompt"));
                yield break;
        }
    }

    /// <summary>The PC's attack-of-opportunity prompt for a catalog action (UiActionDecider). False when a prompt is already open.</summary>
    internal bool TryShowActionAoOConfirmation(AoOProvokingActionInfo info)
    {
        if (info == null || _waitingForAoOConfirmation)
            return false;
        ShowAoOActionConfirmation(info);
        return true;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────────────────────────────────

    private static Legality CheckRunnable(ActionContext ctx)
    {
        if (ctx == null || ctx.Definition == null)
            return Legality.Refused(LegalityBar.Other, "no catalog action");
        if (ctx.Actor == null || ctx.Actor.Stats == null)
            return Legality.Refused(LegalityBar.Target, "no actor");
        if (ctx.Definition.Executor == null)
        {
            string issues = ctx.Definition.IssueIds != null && ctx.Definition.IssueIds.Length > 0
                ? " (" + string.Join(", ", ctx.Definition.IssueIds) + ")"
                : string.Empty;
            return Legality.Refused(LegalityBar.Other, ctx.Definition.Name + " has no shared executor yet: " + ctx.Definition.Status + issues);
        }
        return Legality.Allowed();
    }

    /// <summary>Extracted and New entries are pre-checked (their CanExecute is the legality); a Delegating entry's core owns its refusals.</summary>
    private static bool IsPreChecked(ActionDefinition def)
        => def != null && (def.Status == ImplStatus.Extracted || def.Status == ImplStatus.New);

    private void FinishActionOutcome(ActionContext ctx, ActionOutcome o, ActionCosts.Snapshot before)
    {
        o.Spent = ActionCosts.SpentSince(before, ctx.Actor);
        if (CombatEndRules.IsOutOfFight(ctx.Actor))
            o.ActorDown = true;
        if (CurrentPhase == TurnPhase.CombatOver)
            o.CombatEnded = true;
    }

    /// <summary>The shadow hook (step 6 compares the pick with the legal set): a Debug.Log line when the actor cannot start the pick.</summary>
    private static void ActionShadowCheck(ActionContext ctx)
    {
        Legality start = ctx.Definition.Executor.CanStart(ctx.Actor);
        if (!start.Ok)
            Debug.Log($"[ActionSet] shadow: {DescribeAction(ctx)} is outside what its actor can start now: {start.Reason} ({start.Bar})");
    }

    private void ShowActionRefusal(CharacterController actor, ActionContext ctx, Legality refusal)
    {
        string who = actor != null && actor.Stats != null ? actor.Stats.CharacterName : "?";
        string what = ctx != null && ctx.Definition != null ? ctx.Definition.Name : "that action";
        string reason = refusal.Reason != null ? refusal.Reason.TrimEnd().TrimEnd('.') : string.Empty;
        if (string.IsNullOrEmpty(reason))
            return;
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{who} cannot {what.ToLowerInvariant()}: {reason}."));
    }

    private static string DescribeAction(ActionContext ctx)
    {
        string who = ctx != null && ctx.Actor != null && ctx.Actor.Stats != null ? ctx.Actor.Stats.CharacterName : "?";
        string id = ctx != null && ctx.Definition != null ? ctx.Definition.Id : "?";
        return who + " " + id;
    }
}
