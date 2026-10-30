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
/// (FrightenedBehaviorController.ExecuteDecision) through RunAIAction. Since step 6 every AI action call runs through
/// RunAIAction: the GameManager *ForAI wrappers (attack, charge, move, withdraw, 5-foot step, stand up, overrun, breath),
/// the NPC cast and maneuver cores, AIService's ranged specials and engulf, lay on hands and bardic music. Since step 18
/// the breath, spittle, ranged specials and engulf are shared too: a controllable creature takes them through its Monster
/// Abilities button (RunPlayerAction).
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
        // An AiOnly entry has an executor for the AI's routing only (ImplStatus.AiOnly); none is left since batch 13 step 18
        // made the monster specials shared, but the gate stays for any later one.
        if (refusal.Ok && ctx.Definition.Status == ImplStatus.AiOnly)
            refusal = Legality.Refused(LegalityBar.Other, ctx.Definition.Name + " has no PC path yet (AI-only)");
        if (refusal.Ok && IsPreChecked(ctx.Definition))
            refusal = ctx.Definition.Executor.CanExecute(ctx);
        if (!refusal.Ok)
        {
            o.Refuse(refusal);
            ShowActionRefusal(actor, ctx, refusal);
            ShowActionChoices();
            yield break;
        }

        // A move or withdraw walks over many frames: the PC is busy until the entry's tail returns the action panel, as
        // the click path's ExecuteMovement marks it (batch 13 step 9 review; the harness's Ui Step.Action move waits on it).
        if (ctx.Definition.Executor is MoveActionExecutor)
        {
            CurrentSubPhase = PlayerSubPhase.Animating;
            if (_pathPreview != null) _pathPreview.HidePath();
            if (_hoverMarker != null) _hoverMarker.Hide();
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

        ActionSetShadowCheck(ctx);

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
    /// <see cref="ActionOutcome.ActorDown"/> and <see cref="ActionOutcome.CombatEnded"/>. <paramref name="payOnProceed"/>, when
    /// given, pays the action once the actor goes on (with nobody threatening, at once; after the prompt otherwise) and
    /// before any attack is taken (an action is paid when it is taken, D3, D6); false refuses it with no attack taken.
    /// <paramref name="afterEachAttack"/>, when given, sees each attack's result once it is logged, while the actor still
    /// stands (the drink's Concentration check after a hit, DMG p.229; batch 13 step 14).
    /// </summary>
    internal IEnumerator ResolveActionProvocation(CharacterController actor, AoOProvokingAction action, IActionDecider decider, ActionOutcome outcome,
        string actionName = null, System.Func<bool> payOnProceed = null, System.Action<CombatResult> afterEachAttack = null)
    {
        if (outcome == null)
            outcome = new ActionOutcome();
        if (actor == null || actor.Stats == null)
            yield break;

        List<CharacterController> provokers = ThreatSystem.GetActionAoOProvokers(actor, GetAllCharacters());
        if (provokers.Count == 0)
        {
            if (payOnProceed != null && !payOnProceed())
                outcome.Refuse(Legality.Refused(LegalityBar.Action, "the action could not be paid"));
            yield break;
        }

        yield return ConfirmActionProvocation(actor, action, actionName, provokers, decider, outcome);
        if (outcome.Cancelled || outcome.CombatEnded || !outcome.Refusal.Ok)
            yield break;
        if (payOnProceed != null && !payOnProceed())
        {
            outcome.Refuse(Legality.Refused(LegalityBar.Action, "the action could not be paid"));
            yield break;
        }

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
            if (afterEachAttack != null && !CombatEndRules.IsOutOfFight(actor))
                afterEachAttack(aoo);
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

    /// <summary>
    /// The legal set in shadow mode (batch 13 step 6, design choice C8): the pick is compared with the actor's
    /// <see cref="ActorActionSet"/> (today's legality through the ActionLegality adapters, the compulsion filter and the
    /// charm links), and a disagreement is a Debug.Log line only, never a combat-log line (combat-log lines are hashed).
    /// Nothing reads the set's answer yet: the pick runs either way. The set is pure (no dice, no log, no state change); a
    /// throw while building it is recorded and ignored, so the shadow can never change a turn. Each disagreement also goes
    /// to <see cref="ActionSetShadow"/>, which the scenario harness writes into its results (fast mode drops Log-level
    /// lines). Skipped where <see cref="ActionSetShadow.Enabled"/> is false (a release player build).
    /// </summary>
    internal void ActionSetShadowCheck(ActionContext ctx)
    {
        if (!ActionSetShadow.Enabled)
            return;
        try
        {
            ActorActionSet set = ActorActionSet.Build(this, ctx.Actor, ActorSource.AI);
            Legality legal = set.Check(ctx.Definition.Id, ctx);
            if (!legal.Ok)
            {
                ActionSetShadow.Record(ctx.Actor.Stats.CharacterName, ctx.Definition.Id, legal.Bar.ToString(), legal.Reason);
                Debug.Log($"[ActionSet] {ctx.Actor.Stats.CharacterName} picked {ctx.Definition.Id} outside the legal set: {legal.Reason} ({legal.Bar})");
            }
        }
        catch (System.Exception ex)
        {
            ActionSetShadow.Record(ctx.Actor != null && ctx.Actor.Stats != null ? ctx.Actor.Stats.CharacterName : "?",
                ctx.Definition != null ? ctx.Definition.Id : "?", "Error", ex.GetType().Name + ": " + ex.Message);
            Debug.Log($"[ActionSet] the legal set of {DescribeAction(ctx)} could not be built: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// <see cref="RunAIAction"/> for a caller that yields it in place of the core it used to yield (batch 13 step 6, a pure
    /// move): the runner's and the executor's enumerators are inlined (<see cref="InlineAction"/>, two levels), so the
    /// core's own yields reach the caller's coroutine exactly as before and no frame is added.
    /// </summary>
    internal IEnumerator RunAIActionInline(ActionContext ctx, ActionOutcome o)
        => InlineAction(RunAIAction(ctx, o ?? new ActionOutcome()), 2);

    /// <summary>
    /// <see cref="RunPlayerAction"/> for a PC coroutine that yields it in place of the core it used to run (batch 13 step 9,
    /// the PC's move and withdraw, a pure move): the runner's and the executor's enumerators are inlined as in
    /// <see cref="RunAIActionInline"/>, so the core's own yields are the caller's and no frame is added.
    /// </summary>
    internal IEnumerator RunPlayerActionInline(ActionContext ctx, ActionOutcome o)
    {
        if (ctx != null)
            ctx.Source = ActorSource.Ui;
        return InlineAction(RunPlayerActionRoutine(ctx, o ?? new ActionOutcome()), 2);
    }

    /// <summary>
    /// Runs <see cref="RunAIAction"/> to its end at once, for a synchronous AI call site (a cast through RunCastSync, a
    /// maneuver with <see cref="ActionContext.NoWait"/>, a 5-foot step, lay on hands, bardic music, the monster specials):
    /// every nested enumerator is drained in place. An executor that waits (a non-enumerator yield) is a caller's bug: it is
    /// logged and its waits are skipped, with a step cap so a wait loop cannot hang the turn. Returns the outcome.
    /// </summary>
    internal ActionOutcome RunAIActionNow(ActionContext ctx, ActionOutcome o = null)
    {
        if (o == null)
            o = new ActionOutcome();
        var stack = new Stack<IEnumerator>();
        stack.Push(RunAIAction(ctx, o));
        int steps = 0;
        bool warned = false;
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext())
            {
                stack.Pop();
                continue;
            }
            if (top.Current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"[ActionRunner] {DescribeAction(ctx)} waited inside a synchronous run ({top.Current}); its waits are skipped");
            }
            if (++steps > 10000)
            {
                Debug.LogWarning($"[ActionRunner] {DescribeAction(ctx)} did not finish in a synchronous run; stopped");
                break;
            }
        }
        return o;
    }

    /// <summary>
    /// Inlines <paramref name="root"/> and the enumerators it yields down to <paramref name="levels"/> levels below it: their
    /// leaf yields (null, WaitForSeconds, a Coroutine) and the enumerators deeper than that pass to the caller unchanged.
    /// </summary>
    internal static IEnumerator InlineAction(IEnumerator root, int levels)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext())
            {
                stack.Pop();
                continue;
            }
            object current = top.Current;
            if (current is IEnumerator nested && stack.Count <= levels)
            {
                stack.Push(nested);
                continue;
            }
            yield return current;
        }
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
