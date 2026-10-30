using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Delegating executors of the action catalog (batch 13 step 3, a pure move): each wraps a finished actor-agnostic
/// core and adds nothing to its rules. The core owns its refusals, its payment and its log; the executor builds the
/// core's request from the context when the caller gave none (by the context's Source, never by who controls the actor),
/// runs it as a nested enumerator (a callback core: waits on its flag, re-checking the combat end) and fills the
/// outcome. No executor continues the UI or reads IsControllable, ActivePC or IsPlayerTurn (ActionCatalogTests' scan).
/// </summary>
internal static class ExecutorUtil
{
    internal static Legality Alive(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return Legality.Refused(LegalityBar.Target, "no actor");
        if (CombatEndRules.IsOutOfFight(actor))
            return Legality.Refused(LegalityBar.Condition, actor.Stats.CharacterName + " is out of the fight");
        return Legality.Allowed();
    }

    internal static Legality LiveTarget(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return Legality.Refused(LegalityBar.Target, "no target");
        if (target.Stats.IsDead)
            return Legality.Refused(LegalityBar.Target, "the target is gone");
        return Legality.Allowed();
    }

    internal static GameManager Gm => GameManager.Instance;

    internal static bool CombatOver(GameManager gm) => gm == null || gm.CurrentPhase == GameManager.TurnPhase.CombatOver;
}

/// <summary>Attack, full attack and flurry of blows: GameManager.RunAttackSequence (batch 12 step 17).</summary>
public sealed class AttackSequenceActionExecutor : IActionExecutor
{
    public enum Kind { Attack, FullAttack, Flurry }

    private readonly Kind _kind;

    public AttackSequenceActionExecutor(Kind kind) { _kind = kind; }

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        switch (_kind)
        {
            case Kind.Flurry:
                return ActionLegality.FromCheck(actor.CanUseFlurryOfBlows(out string flurryReason), flurryReason, LegalityBar.Action);
            case Kind.FullAttack:
                return actor.Actions.HasFullRoundAction || actor.ProgressiveAttackPool.HasStartedAttacking
                    ? Legality.Allowed()
                    : Legality.Refused(LegalityBar.Action, "a full attack needs the full-round action (PHB p.143)");
            default:
                return ActionCosts.CanAfford(actor, ActionCostType.Standard) || actor.ProgressiveAttackPool.HasStartedAttacking
                    ? Legality.Allowed()
                    : Legality.Refused(LegalityBar.Action, "no attack action left this turn (PHB p.139)");
        }
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        var req = ctx.CoreRequest as AttackSequenceRequest;
        return ExecutorUtil.LiveTarget(req != null ? req.InitialTarget : ctx.Target);
    }

    /// <summary>
    /// Three ways in, each the rules of a live caller:
    /// - a caller's own AttackSequenceRequest (ctx.CoreRequest): the caller already ran the attack prelude, so only the
    ///   sequence runs (RunAttackSequence), after the flurry declaration;
    /// - the AI without one (or with an NpcAttackRequest, the AI wrappers' step cap): GameManager.NPCPerformAttack
    ///   (ActionCore_NpcAttack), the AI's attack with its whole prelude (the grapple gate,
    ///   PHB p.156; the hold bar, MM p.310; the Sphere and ward gate; the ward break, PHB p.274, p.241; frightful presence;
    ///   the weapon fallback and reload; the profile's smite and flurry declarations) and its post-sequence steps, with the
    ///   context's maneuver hook (CMB-102). The attack entry caps it at one step; the full attack takes every step the
    ///   action economy allows. It logs why it made no attack;
    /// - the player without one: CombatFlowService.PerformPlayerAttack's rules without its UI: the direct-attack gate
    ///   (AttackLegality: Sphere, HoldBar, Ward), the ward break, then the sequence with the player's choices.
    /// A flurry is declared first (DeclareFlurryOfBlows: the full-round action and the declaration line, PHB p.40), and the
    /// request is built after it, so a flurry never carries a two-weapon intent (PHB p.40).
    /// </summary>
    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        CharacterController actor = ctx.Actor;
        if (gm == null || actor == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no attack request"));
            yield break;
        }

        var callerRequest = ctx.CoreRequest as AttackSequenceRequest;
        if (callerRequest == null && ctx.Source == ActorSource.Ui)
        {
            Legality gate = AttackLegality.Check(actor, ctx.Target, DirectAttackKind.Weapon, null,
                AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward | AttackLegalityChecks.Charm);
            if (!gate.Ok)
            {
                o.Refuse(gate);
                yield break;
            }
            // Sanctuary and Hide from Undead end when the warded creature attacks (PHB p.274, p.241).
            CombatFlowService.BreakProtectiveWardsOnAttack(actor);
        }

        if (_kind == Kind.Flurry && !actor.IsUsingFlurryOfBlows && !gm.DeclareFlurryOfBlows(actor, ctx.Target, out string flurryReason))
        {
            o.Refuse(Legality.Refused(LegalityBar.Action, flurryReason));
            yield break;
        }

        if (callerRequest == null && ctx.Source == ActorSource.AI)
        {
            ActionCosts.Snapshot before = ActionCosts.Take(actor);
            int maxSteps = ctx.CoreRequest is NpcAttackRequest npcRequest ? npcRequest.MaxSteps : (_kind == Kind.Attack ? 1 : int.MaxValue);
            yield return gm.ActionCore_NpcAttack(actor, ctx.Target, ctx.TryStepManeuver, maxSteps);
            o.Performed = ActionCosts.SpentSince(before, actor) != ActionCostType.Free || _kind == Kind.Flurry;
            if (!o.Performed)
            {
                o.Refuse(Legality.Refused(LegalityBar.Other, "no attack was made"));
                o.RefusalLogged = true;
            }
            yield break;
        }

        AttackSequenceRequest req = callerRequest ?? Build(gm, ctx);
        // A flurry has no off-hand attacks (PHB p.40): a two-weapon request built before it was declared is rebuilt.
        if (_kind == Kind.Flurry && req != null && req.InterleaveOffHand)
            req = Build(gm, ctx);
        if (req == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no attack request"));
            yield break;
        }

        var seq = new AttackSequenceOutcome();
        yield return gm.RunAttackSequence(req, seq);
        o.CoreOutcome = seq;
        o.Performed = seq.AttacksMade > 0 || seq.StepsLostToWard > 0 || seq.ManeuversUsed > 0 || _kind == Kind.Flurry;
        if (!o.Performed)
            o.Refuse(Legality.Refused(seq.StoppedByLegality ? LegalityBar.Target : LegalityBar.Other,
                string.IsNullOrEmpty(seq.StopReason) ? "no attack was made" : seq.StopReason));
    }

    private AttackSequenceRequest Build(GameManager gm, ActionContext ctx)
    {
        if (gm == null || ctx.Actor == null)
            return null;
        bool full = _kind != Kind.Attack;
        if (ctx.Source == ActorSource.Ui)
            return full
                ? AttackSequenceRequest.ForUiFullAttack(gm, ctx.Actor, ctx.Target)
                : AttackSequenceRequest.ForUiAttack(gm, ctx.Actor, ctx.Target);
        AttackSequenceRequest req = AttackSequenceRequest.ForAI(gm, ctx.Actor, ctx.Target, ctx.TryStepManeuver, ctx.Actor.aiProfile);
        if (!full)
            req.MaxSteps = 1;
        return req;
    }
}

/// <summary>Cast a spell: the staged cast core (GameManager.ExecuteCast, RunCastSync; batch 11).</summary>
public sealed class CastActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return actor.Spellcasting != null
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Resource, "no spellcasting");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        var req = ctx.CoreRequest as SpellCastRequest;
        GameManager gm = ExecutorUtil.Gm;
        if (req == null || gm == null)
            return Legality.Refused(LegalityBar.Other, "no cast request");
        return ActionLegality.FromCast(gm.CheckLegality(req));
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        var req = ctx.CoreRequest as SpellCastRequest;
        if (gm == null || req == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no cast request"));
            yield break;
        }
        yield return Run(gm, req, o);
    }

    /// <summary>Runs <paramref name="req"/> through the cast core with its own decider and reads the outcome.</summary>
    internal static IEnumerator Run(GameManager gm, SpellCastRequest req, ActionOutcome o)
    {
        CastOutcome finished = null;
        if (req.Decider != null && req.Decider.IsSynchronous)
        {
            gm.RunCastSync(req, out finished);
        }
        else
        {
            // The UI decider may suspend the run at a dialog or the AoO prompt; its own finish continuation does the UI
            // follow-up (GameManager.CastDeciders.cs), so the PC runner adds none (CoreOwnsUi). A dialog that re-enters
            // the cast replaces the done callback (ExecuteCast), so the context's own outcome is read when it ends.
            o.CoreOwnsUi = true;
            CastContext castCtx = gm.BeginCast(req);
            gm.ExecuteCast(castCtx, x => finished = x);
            while (finished == null && !castCtx.Ended && !ExecutorUtil.CombatOver(gm))
                yield return null;
            if (finished == null)
                finished = castCtx.Outcome;
        }

        o.CoreOutcome = finished;
        o.Performed = finished != null && finished.WasCast;
        if (o.Performed)
            yield break;
        // The cast core logs its own refusals and cancels (FinishCast).
        o.RefusalLogged = true;
        if (finished == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "the cast did not finish"));
            yield break;
        }
        o.Refuse(finished.Kind == CastOutcomeKind.Refused
            ? ActionLegality.FromCast(finished)
            : Legality.Refused(LegalityBar.Other, finished.ToString()));
        o.Cancelled = finished.Kind == CastOutcomeKind.Cancelled;
    }
}

/// <summary>Trip, disarm, grapple, sunder, bull rush, feint and coup de grace: GameManager.ExecuteManeuver (batch 12 step 19).</summary>
public sealed class ManeuverActionExecutor : IActionExecutor
{
    private readonly SpecialAttackType _type;

    public ManeuverActionExecutor(SpecialAttackType type) { _type = type; }

    public SpecialAttackType Type => _type;

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        ManeuverCost cost = ManeuverActionCost.For(_type, actor);
        bool afford = cost.ReplacesMeleeAttack
            ? ActionCosts.CanAfford(actor, ActionCostType.Standard) || actor.ProgressiveAttackPool.HasStartedAttacking
            : ActionCosts.CanAfford(actor, cost.Action);
        return afford ? Legality.Allowed() : Legality.Refused(LegalityBar.Action, "no action left for " + _type + " (" + cost + ")");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        GameManager gm = ExecutorUtil.Gm;
        var req = ctx.CoreRequest as ManeuverRequest;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        if (req == null)
        {
            Legality target = ExecutorUtil.LiveTarget(ctx.Target);
            if (!target.Ok)
                return target;
            // Built without the AI's item choice (a disarm or sunder slot): the core's own legality, no decider call.
            req = new ManeuverRequest
            {
                Attacker = ctx.Actor,
                Target = ctx.Target,
                Type = _type,
                Intent = AttackIntent.ForManeuver(_type),
                Cost = ManeuverActionCost.For(_type, ctx.Actor),
                Source = ctx.Source,
            };
        }
        return gm.CanStartManeuver(req);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        ManeuverRequest req = ctx.CoreRequest as ManeuverRequest;
        if (req == null && gm != null && ctx.Actor != null)
            req = ctx.Source == ActorSource.Ui
                ? ManeuverRequest.ForUi(ctx.Actor, ctx.Target, _type, null, false, null)
                : ManeuverRequest.ForAI(gm, ctx.Actor, ctx.Target, _type, null);
        if (gm == null || req == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no maneuver request"));
            yield break;
        }

        // NoWait (the NPC maneuver path): the outcome is read at once, as GameManager.TryNPCSpecialAttackIfBeneficial
        // always did; a controllable defender's prompt settles later and the caller waits on it itself.
        bool settled = false;
        ManeuverOutcome mo = ctx.NoWait ? gm.ExecuteManeuver(req, null) : gm.ExecuteManeuver(req, _ => settled = true);
        while (!ctx.NoWait && !settled && !ExecutorUtil.CombatOver(gm))
            yield return null;

        o.CoreOutcome = mo;
        o.Performed = mo != null && mo.Spent;
        o.ActorDown = mo != null && mo.AttackerDown;
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, mo != null && !string.IsNullOrEmpty(mo.Reason) ? mo.Reason : "the maneuver was refused"));
    }
}

/// <summary>Charge and the charge bull rush: CanCharge (its path test TryBuildChargePath, the owner's one charge legality test) and ExecuteCharge (batch 12 step 21).</summary>
public sealed class ChargeActionExecutor : IActionExecutor
{
    private readonly bool _bullRush;

    public ChargeActionExecutor(bool bullRush) { _bullRush = bullRush; }

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return ActionLegality.FromCheck(ChargeRules.HasChargeAction(actor, out _, out string reason), reason, LegalityBar.Action);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        GameManager gm = ExecutorUtil.Gm;
        ChargeRequest req = ctx.CoreRequest as ChargeRequest ?? Build(gm, ctx);
        if (gm == null || req == null)
            return Legality.Refused(LegalityBar.Other, "no charge request");
        return gm.CanCharge(req);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        ChargeRequest req = ctx.CoreRequest as ChargeRequest ?? Build(gm, ctx);
        if (gm == null || req == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no charge request"));
            yield break;
        }

        var co = new ChargeOutcome();
        yield return gm.ExecuteCharge(req, co);
        o.CoreOutcome = co;
        o.Performed = co.Started;
        o.ActorDown = co.ChargerDropped;
        o.Provoked = co.InterruptedByAoO;
        if (co.Refused)
            o.Refuse(Legality.Refused(LegalityBar.Other, co.RefusedReason));
    }

    private ChargeRequest Build(GameManager gm, ActionContext ctx)
    {
        if (gm == null || ctx == null || ctx.Actor == null)
            return null;
        return ctx.Source == ActorSource.Ui
            ? gm.ChargeRequestForUi(ctx.Actor, ctx.Target, _bullRush)
            : gm.ChargeRequestForAI(ctx.Actor, ctx.Target, _bullRush);
    }
}

/// <summary>The request of an overrun move (GameManager.RunOverrunMove): the path, the AoOs it provokes and the step timing.</summary>
public sealed class OverrunActionRequest
{
    public List<Vector2Int> Path;

    /// <summary>The AoOs the path provokes; null: GameManager.CheckForAoO on the path, as the live callers compute it.</summary>
    public List<AoOThreatInfo> ProvokedAoOs;
    public float SecondsPerStep = 0.08f;

    /// <summary>The caller's own outcome to fill (the NPC path's, GameManager.NPCExecuteOverrunForAI); null: a new one.</summary>
    public OverrunMoveOutcome Outcome;

    /// <summary>The combat-end check after a started overrun; null: the caller checks (the NPC path checks always, "Overrun.NPC").</summary>
    public string CombatEndContext = "ActionCatalog.Overrun";
}

/// <summary>Overrun: the one overrun move, GameManager.RunOverrunMove (PHB p.157-158; CMB-015, CMB-086).</summary>
public sealed class OverrunActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromCheck(gm.CanUseOverrun(actor, out string reason), reason, LegalityBar.Action);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        var req = ctx.CoreRequest as OverrunActionRequest;
        return req != null && req.Path != null && req.Path.Count > 0
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Terrain, "no overrun path");
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        var req = ctx.CoreRequest as OverrunActionRequest;
        if (gm == null || req == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Terrain, "no overrun path"));
            yield break;
        }

        // Leaving threatened squares on the way provokes (PHB p.137), as both live callers pass it.
        List<AoOThreatInfo> provoked = req.ProvokedAoOs ?? gm.CheckForAoO(ctx.Actor, req.Path);
        OverrunMoveOutcome oo = req.Outcome ?? new OverrunMoveOutcome();
        yield return gm.RunOverrunMove(ctx.Actor, req.Path, provoked, req.SecondsPerStep, oo);
        o.CoreOutcome = oo;
        o.Performed = oo.Started;
        o.ActorDown = oo.AttackerDown;
        if (!oo.Started)
            o.Refuse(Legality.Refused(LegalityBar.Other, string.IsNullOrEmpty(oo.StopReason) ? "the overrun did not start" : oo.StopReason));
        // RunOverrunMove leaves the combat-end check to its caller.
        if (oo.Started && req.CombatEndContext != null)
            gm.EvaluateCombatEnd(req.CombatEndContext);
    }
}

/// <summary>
/// Move and withdraw (status Extracted, batch 13 step 9): the one move executor of both sides,
/// GameManager.ExecuteMoveAction, run with the context's <see cref="MoveRequest"/> (<see cref="ActionContext.CoreRequest"/>:
/// the PC's MoveRequest.ForUi, the AI wrappers' MoveRequest.ForAI and ForAIWithdraw). With no request it builds one for
/// the context's side: for <see cref="ActorSource.Ui"/> (the harness's Ui Step.Action) the PC's own (<see cref="UiRequestFor"/>:
/// the click path's search, MoveRequest.ForUi, paid before the walk, the standard action converted); for an AI pick through
/// the catalog (AiOutOfReach) the AI's: a move paid before its first step (D3, batch 13 step 10), a withdraw that pays its
/// own action. <see cref="CanExecute"/> asks only for a destination: the movement legality is the
/// executor's first step, which logs its refusal as the AI cores always did (CanStart asks it for the legal set).
/// </summary>
public sealed class MoveActionExecutor : IActionExecutor
{
    private readonly bool _withdraw;

    public MoveActionExecutor(bool withdraw) { _withdraw = withdraw; }

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        MoveKind kind = _withdraw ? MovementService.WithdrawKindFor(actor) : MoveKind.Move;
        return MovementService.CanStartMovement(actor, kind);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        if (ctx.Actor == null || ctx.Actor.Stats == null)
            return Legality.Refused(LegalityBar.Target, "no actor");
        var req = ctx.CoreRequest as MoveRequest;
        bool hasWhere = ctx.Cell.HasValue || (req != null && (req.Destination.HasValue || (req.Path != null && req.Path.Count > 0)));
        return hasWhere ? Legality.Allowed() : Legality.Refused(LegalityBar.Terrain, "no destination");
    }

    /// <summary>The request the catalog's own AI pick runs (no MoveRequest on the context): a move spent before its first
    /// step, as every move is (D3); or the AI withdraw.</summary>
    internal static MoveRequest DefaultAiRequest(bool withdraw, CharacterController mover, Vector2Int destination, float secondsPerStep)
        => withdraw
            ? MoveRequest.ForAIWithdraw(mover, destination, secondsPerStep)
            : MoveRequest.ForAI(mover, destination, secondsPerStep, MoveSpend.MoveAction);

    /// <summary>
    /// The request a PC's catalog move with no MoveRequest runs (a Ui context: the harness's Step.Action), the PC's own
    /// rule as its click builds it (GameManager.HandleMovementClick): the path the click would find (the move or withdraw
    /// budget, threats avoided, through allies, a withdraw's start square exempt, PHB p.143), MoveRequest.ForUi on it. No
    /// path (none, or no budget): an empty path, so the executor's legality still logs its refusal and nothing is spent.
    /// </summary>
    internal static MoveRequest UiRequestFor(GameManager gm, bool withdraw, CharacterController actor, Vector2Int destination)
    {
        var path = new List<Vector2Int>();
        List<AoOThreatInfo> provoked = null;
        if (gm != null && actor != null && actor.Stats != null && destination != actor.GridPosition)
        {
            int budget = withdraw ? gm.GetWithdrawMoveRangeSquares(actor) : gm.GetCurrentMoveRangeSquares(actor);
            if (budget > 0)
            {
                AoOPathResult found = gm.FindPath(actor, destination, avoidThreats: true, maxRangeOverride: budget,
                    allowThroughAllies: true, allowThroughEnemies: false, suppressFirstSquareAoO: withdraw);
                if (found != null && found.Path != null && found.Path.Count > 0)
                {
                    path = new List<Vector2Int>(found.Path);
                    provoked = found.ProvokedAoOs;
                }
            }
        }
        return MoveRequest.ForUi(actor, path, provoked, withdraw, gm != null ? gm.GetPlayerMoveSecondsPerStepForAI() : 0.08f);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        var req = ctx.CoreRequest as MoveRequest;
        if (gm == null || (req == null && !ctx.Cell.HasValue))
        {
            o.Refuse(Legality.Refused(LegalityBar.Terrain, "no destination"));
            yield break;
        }

        // The context's side picks only the rule's builder: a Ui context with no request gets the PC's own request (the
        // PC/NPC parity rule; the harness's Ui Step.Action), never the AI's segment walker.
        if (req == null)
            req = ctx.Source == ActorSource.Ui
                ? UiRequestFor(gm, _withdraw, ctx.Actor, ctx.Cell.Value)
                : DefaultAiRequest(_withdraw, ctx.Actor, ctx.Cell.Value, gm.GetPlayerMoveSecondsPerStepForAI());
        if (req.Mover == null)
            req.Mover = ctx.Actor;
        yield return gm.ExecuteMoveAction(req);
        MoveAttempt attempt = req.Attempt;

        o.CoreOutcome = attempt;
        if (attempt.Incapacitated)
            o.ActorDown = true;
        if (attempt.Refused && !attempt.NoPath)
        {
            // The executor logs its refusal.
            o.Refuse(Legality.Refused(LegalityBar.Other, attempt.Reason));
            o.RefusalLogged = true;
            yield break;
        }
        // No path (a Debug.Log line only, nothing spent, D3): the runner shows a PC's refusal.
        if (!attempt.Started)
        {
            o.Refuse(Legality.Refused(LegalityBar.Terrain, _withdraw ? "the withdrawal did not start" : "the move did not start"));
            yield break;
        }
        o.Performed = true;
    }
}

/// <summary>The 5-foot step: the shared ExecuteFiveFootStep with no UI follow-up (GameManager.ActionCore_FiveFootStep; TryTakeFiveFootStepForAI routes here).</summary>
public sealed class FiveFootStepActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromDisabledReason(gm.GetFiveFootStepDisabledReason(actor), LegalityBar.Action);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        if (!ctx.Cell.HasValue)
            return Legality.Refused(LegalityBar.Terrain, "no destination");
        return ExecutorUtil.Gm.CanTakeFiveFootStepToForAI(ctx.Actor, ctx.Cell.Value)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Terrain, "not a legal 5-foot step destination");
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || !ctx.Cell.HasValue)
        {
            o.Refuse(Legality.Refused(LegalityBar.Terrain, "no destination"));
            yield break;
        }
        o.Performed = gm.ActionCore_FiveFootStep(ctx.Actor, ctx.Cell.Value);
        if (!o.Performed)
        {
            // ExecuteFiveFootStep logs why the step failed.
            o.Refuse(Legality.Refused(LegalityBar.Terrain, "the 5-foot step was not taken"));
            o.RefusalLogged = true;
        }
    }
}

/// <summary>Stand up from prone: GameManager.ResolveStandUpFromProne (PHB p.143, Table 8-2; CMB-074).</summary>
public sealed class StandUpActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromDisabledReason(gm.GetStandUpDisabledReason(actor), LegalityBar.Condition);
    }

    public Legality CanExecute(ActionContext ctx) => ctx == null ? Legality.Refused(LegalityBar.Other, "no context") : CanStart(ctx.Actor);

    /// <summary>
    /// GameManager.ActionCore_StandUp: the two live callers' gate first (prone, GetStandUpDisabledReason; nothing spent on a
    /// refusal), the player's attack-of-opportunity prompt (StandFromProne) when foes threaten, then the shared core.
    /// </summary>
    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        yield return gm.ActionCore_StandUp(ctx.Actor, ctx.Source, ctx.Decider ?? ActionDeciders.For(ctx.Actor, ctx.Source), o);
    }
}

/// <summary>Declare a smite (the paladin's Smite Evil, PHB p.44; the celestial and fiendish smites, MM p.31, p.107): SmiteRules.TryDeclare.</summary>
public sealed class SmiteActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return ActionLegality.FromCheck(SmiteRules.CanDeclare(actor, out _, out string reason), reason, LegalityBar.Resource);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        // Not against a creature held with one body part, which it may not attack (MM p.310; CMB-163).
        if (ctx.Target != null && ctx.Actor.IsBarredFromOrdinaryAttackOn(ctx.Target, out string barred))
            return Legality.Refused(LegalityBar.Target, barred);
        return Legality.Allowed();
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        // The one declaration and its line (GameManager.DeclareSmite). The PC Smite button's extra steps (the turned
        // condition's block, withdrawing a declared smite, CanUseSmiteButton) stay on the button until a PC flow uses this.
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        if (!gm.DeclareSmite(ctx.Actor, out string reason))
        {
            o.Refuse(Legality.Refused(LegalityBar.Resource, reason));
            yield break;
        }
        o.Performed = true;
    }
}

/// <summary>Lay on hands (PHB p.44): the shared executor behind the PC click and the AI (GameManager.TryLayOnHandsForAI).</summary>
public sealed class LayOnHandsActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return ActionLegality.FromCheck(LayOnHandsRules.CanUse(actor, out string reason), reason, LegalityBar.Resource);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        return start.Ok ? ExecutorUtil.LiveTarget(ctx.Target) : start;
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        o.Performed = gm != null && gm.TryLayOnHandsForAI(ctx.Actor, ctx.Target);
        if (!o.Performed)
        {
            // ExecuteLayOnHands logs its refusal.
            o.Refuse(Legality.Refused(LegalityBar.Other, "lay on hands was not used"));
            o.RefusalLogged = gm != null;
        }
        yield break;
    }
}

/// <summary>Wild shape (PHB p.37) and changing back: the shared executors (GameManager.TryWildShapeForAI, TryWildShapeRevertForAI).</summary>
public sealed class WildShapeActionExecutor : IActionExecutor
{
    public const string RevertVariant = "revert";

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        if (WildShapeRules.CanAssume(actor, out string assumeReason))
            return Legality.Allowed();
        return ActionLegality.FromCheck(WildShapeRules.CanRevert(actor, out _), assumeReason, LegalityBar.Resource);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality alive = ExecutorUtil.Alive(ctx.Actor);
        if (!alive.Ok)
            return alive;
        if (ctx.Variant == RevertVariant)
            return ActionLegality.FromCheck(WildShapeRules.CanRevert(ctx.Actor, out string revertReason), revertReason, LegalityBar.Condition);
        if (string.IsNullOrEmpty(ctx.Variant))
            return Legality.Refused(LegalityBar.Target, "no wild shape form chosen");
        return ActionLegality.FromCheck(WildShapeRules.CanAssume(ctx.Actor, out string reason), reason, LegalityBar.Resource);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || string.IsNullOrEmpty(ctx.Variant))
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no wild shape form chosen"));
            yield break;
        }
        o.Performed = ctx.Variant == RevertVariant
            ? gm.TryWildShapeRevertForAI(ctx.Actor)
            : gm.TryWildShapeForAI(ctx.Actor, ctx.Variant);
        if (!o.Performed)
        {
            // ExecuteWildShape and ExecuteWildShapeRevert log their refusals.
            o.Refuse(Legality.Refused(LegalityBar.Other, "wild shape was not used"));
            o.RefusalLogged = true;
        }
    }
}

/// <summary>Bardic music (PHB p.29-30): start, concentrate, suggestion or stop, through the shared executors (BardicMusicSystem).</summary>
public sealed class BardicMusicActionExecutor : IActionExecutor
{
    public const string ConcentrateVariant = "concentrate";
    public const string StopVariant = "stop";
    public const string SuggestionVariant = "suggestion";

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return BardicMusicRules.HasBardicMusic(actor.Stats)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Resource, "no bardic music");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        switch (ctx.Variant)
        {
            case ConcentrateVariant:
                return ActionLegality.FromCheck(BardicMusicRules.CanConcentrate(ctx.Actor, out string cReason), cReason, LegalityBar.Action);
            case StopVariant:
                return Legality.Allowed();
            case SuggestionVariant:
                return ExecutorUtil.LiveTarget(ctx.Target);
            default:
                if (!TryParseStart(ctx.Variant, out BardicAbility ability, out _))
                    return Legality.Refused(LegalityBar.Target, "no bardic music ability chosen");
                return ActionLegality.FromCheck(BardicMusicRules.CanStart(ctx.Actor, ability, out string sReason), sReason, LegalityBar.Resource);
        }
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        switch (ctx.Variant)
        {
            case ConcentrateVariant:
                o.Performed = gm.TryBardicConcentrateForAI(ctx.Actor);
                break;
            case StopVariant:
                o.Performed = gm.TryBardicStopForAI(ctx.Actor);
                break;
            case SuggestionVariant:
                o.Performed = gm.TryBardicSuggestionForAI(ctx.Actor, ctx.Target);
                break;
            default:
                if (TryParseStart(ctx.Variant, out BardicAbility ability, out string skill))
                {
                    // A caller's own list (the AI's targets, AIProfile.ChooseBardicMusic) or the one target.
                    IList<CharacterController> targets = ctx.CoreRequest as IList<CharacterController>
                        ?? (ctx.Target != null ? new List<CharacterController> { ctx.Target } : null);
                    o.Performed = gm.TryBardicMusicForAI(ctx.Actor, ability, targets, skill);
                }
                break;
        }
        if (!o.Performed)
        {
            // ShowBardicResult logs a refusal of a parsed variant; an unknown variant is not logged.
            o.Refuse(Legality.Refused(LegalityBar.Other, "bardic music was not used"));
            o.RefusalLogged = ctx.Variant == ConcentrateVariant || ctx.Variant == StopVariant || ctx.Variant == SuggestionVariant
                || TryParseStart(ctx.Variant, out _, out _);
        }
    }

    /// <summary>A start variant: the ability's name, and for Inspire Competence ":" and the skill ("InspireCompetence:Climb").</summary>
    internal static bool TryParseStart(string variant, out BardicAbility ability, out string skill)
    {
        skill = null;
        ability = BardicAbility.None;
        if (string.IsNullOrEmpty(variant))
            return false;
        int colon = variant.IndexOf(':');
        string name = colon >= 0 ? variant.Substring(0, colon) : variant;
        if (colon >= 0)
            skill = variant.Substring(colon + 1);
        return System.Enum.TryParse(name, out ability) && ability != BardicAbility.None;
    }
}

/// <summary>Concentrate to maintain an active spell (PHB p.176, Table 8-2; decision D2): GameManager.TryMaintainConcentrationForAI.</summary>
public sealed class MaintainConcentrationActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return ActionLegality.FromCheck(ConcentrationRules.CanMaintain(actor, out string reason), reason, LegalityBar.Resource);
    }

    public Legality CanExecute(ActionContext ctx) => ctx == null ? Legality.Refused(LegalityBar.Other, "no context") : CanStart(ctx.Actor);

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        o.Performed = gm != null && gm.TryMaintainConcentrationForAI(ctx.Actor);
        if (!o.Performed)
        {
            // ExecuteMaintainConcentration logs its refusal. The PC Maintain button's EndAttackSequence first stays on
            // the button until a PC flow uses this.
            o.Refuse(Legality.Refused(LegalityBar.Other, "the concentration was not maintained"));
            o.RefusalLogged = gm != null;
        }
        yield break;
    }
}

/// <summary>
/// Deliver a held charge (PHB p.141; SPL-156): the AI's delivery (the cast core with HeldChargeDeliveryRules' cost); a
/// caller's own delivery request (the Discharge button's) runs through the cast core with its decider.
/// </summary>
public sealed class DeliverHeldChargeActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return HeldChargeDeliveryRules.CanDeliverNow(actor)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Resource, "no held charge to deliver");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        return start.Ok ? ExecutorUtil.LiveTarget(ctx.Target) : start;
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        if (ctx.CoreRequest is SpellCastRequest req)
        {
            yield return CastActionExecutor.Run(gm, req, o);
            yield break;
        }
        if (ctx.Source == ActorSource.Ui)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "a PC delivery needs the Discharge button's request"));
            yield break;
        }
        o.Performed = gm.TryNPCDeliverHeldChargeForAI(ctx.Actor, ctx.Target);
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, "the held charge was not delivered"));
    }
}
