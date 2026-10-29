using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who pays for a movement run by <see cref="GameManager.ExecuteMoveAction"/> (batch 13 step 9).
/// </summary>
public enum MoveSpend
{
    /// <summary>The executor spends one move action (the standard action converted to a move when
    /// <see cref="MoveRequest.AllowConvertStandard"/> is set and the move action is gone).</summary>
    MoveAction,
    /// <summary>The executor spends nothing: the caller pays otherwise (a frightened creature's run pays its full-round
    /// action; a forced move; a routine whose after-walk test the quirk bits do not express).</summary>
    None,
    /// <summary>The movement pays its own action in the executor: the withdraw's full-round action, or a Restricted
    /// Withdraw's standard action (PHB p.143-144; GameManager.TrySpendWithdrawAction).</summary>
    ExecutorOwned
}

/// <summary>
/// The kept differences of the AI's move sites from the one move rule (batch 13 step 9; the migration rule's named quirk
/// bits, alive from step 9 to step 10 only). <see cref="MoveRequest.ForAI"/> sets them all, <see cref="MoveRequest.ForUi"/>
/// none. Step 10 (AI-076, D3: a move action is spent when the movement starts) deletes the spend bits.
/// </summary>
[Flags]
public enum MoveQuirk
{
    None = 0,
    /// <summary>The move action is spent after the walk, not before it (AI-076): today's AI routines pay once the walk is over.</summary>
    AiSpendsAfterWalk = 1,
    /// <summary>With <see cref="AiSpendsAfterWalk"/>: a move that found no path (or nowhere to go) is spent too, as the AI
    /// routines pay whenever the movement legality did not refuse the move.</summary>
    AiSpendsWithoutPath = 2,
    /// <summary>The walk is the AI's segment walker (GameManager.ExecutePathWithMovementAoOs) rather than the PC's per-step
    /// loop. The two apply the same rules (docs/architecture/combat-and-grid.md); they differ in their log lines, their
    /// waits after an attack of opportunity and the trace type of the steps, which the hashes pin.</summary>
    AiSegmentLoop = 4,
    /// <summary>With <see cref="AiSpendsAfterWalk"/>: no spend when the mover can no longer act after the walk
    /// (CharacterController.CanTakeTurnActions false: dropped by an attack of opportunity or area damage, CMB-073), as the
    /// NPC, summon and frightened routines check before they pay.</summary>
    AiNoSpendIfDropped = 8,
    /// <summary>With <see cref="AiSpendsAfterWalk"/>: no spend once the combat is over (CORE-011), as the NPC and
    /// frightened routines check before they pay.</summary>
    AiNoSpendIfCombatOver = 16,

    /// <summary>Every AI bit: the rule of the AIService routines.</summary>
    AiDefault = AiSpendsAfterWalk | AiSpendsWithoutPath | AiSegmentLoop | AiNoSpendIfDropped | AiNoSpendIfCombatOver
}

/// <summary>
/// One movement request for the one move executor of both sides (<see cref="GameManager.ExecuteMoveAction"/>, batch 13
/// step 9): the PC's move or withdraw (<see cref="ForUi"/>: the clicked path, and the attacks of opportunity it provokes)
/// and the AI's (<see cref="ForAI"/>, <see cref="ForAIWithdraw"/>: a destination, the path found by the executor). The
/// catalog's move and withdraw entries carry it as <see cref="ActionContext.CoreRequest"/>.
/// </summary>
public sealed class MoveRequest
{
    public CharacterController Mover;
    /// <summary><see cref="MoveKind.Move"/> or <see cref="MoveKind.Withdraw"/>; a withdraw walks as
    /// <see cref="MovementService.WithdrawKindFor"/> says (a Restricted Withdraw for a creature limited to one action).</summary>
    public MoveKind Kind = MoveKind.Move;
    /// <summary>The path to walk (the PC's click); null: the executor finds one to <see cref="Destination"/>.</summary>
    public List<Vector2Int> Path;
    /// <summary>The attacks of opportunity <see cref="Path"/> provokes (null: none). Ignored when the executor finds the path.</summary>
    public List<AoOThreatInfo> ProvokedAoOs;
    public Vector2Int? Destination;
    public float SecondsPerStep;
    /// <summary>The attack-of-opportunity log label ("AoO", "AoO (Withdraw)").</summary>
    public string Label = "AoO";
    /// <summary>With no move action left, the standard action is converted to a move (PHB p.141; the PC's move, the compulsion moves).</summary>
    public bool AllowConvertStandard;
    public MoveSpend Spend = MoveSpend.MoveAction;
    public MoveQuirk Quirks;
    /// <summary>When set, a mover dropped on the way runs the combat-end check with this context (the PC's move).</summary>
    public string CombatEndContext;
    /// <summary>The one attempt record, which the executor fills (null: the executor makes one and sets it here); callers
    /// read it after the run.</summary>
    public MoveAttempt Attempt;
    /// <summary>A spend refusal is logged in the PC's wording from before step 9 ("X has no action available for
    /// movement.", "X cannot complete Withdraw: reason."); set by <see cref="ForUi"/>. A legality refusal reads "X cannot
    /// move: reason." for every mover.</summary>
    public bool PcRefusalText;

    public bool IsWithdraw => Kind == MoveKind.Withdraw || Kind == MoveKind.RestrictedWithdraw;

    public bool Has(MoveQuirk quirk) => (Quirks & quirk) == quirk;

    /// <summary>The PC's move or withdraw along the clicked <paramref name="path"/>: spent before the first step (the move
    /// action, or the standard action converted; a withdraw's own action), walked by the per-step loop. No quirk bit.</summary>
    public static MoveRequest ForUi(CharacterController pc, List<Vector2Int> path, List<AoOThreatInfo> provokedAoOs, bool withdraw, float secondsPerStep, MoveAttempt attempt = null)
    {
        return new MoveRequest
        {
            Mover = pc,
            Kind = withdraw ? MoveKind.Withdraw : MoveKind.Move,
            Path = path,
            ProvokedAoOs = provokedAoOs,
            Destination = path != null && path.Count > 0 ? path[path.Count - 1] : (Vector2Int?)null,
            SecondsPerStep = secondsPerStep,
            Label = "AoO",
            AllowConvertStandard = true,
            Spend = withdraw ? MoveSpend.ExecutorOwned : MoveSpend.MoveAction,
            Quirks = MoveQuirk.None,
            CombatEndContext = "PCMovement.Incapacitated",
            PcRefusalText = true,
            Attempt = attempt
        };
    }

    /// <summary>An NPC, summon or compulsion move (one move action's budget) to <paramref name="destination"/>, with every AI
    /// quirk bit (<see cref="MoveQuirk.AiDefault"/>). <paramref name="spend"/> None: the caller pays.</summary>
    public static MoveRequest ForAI(CharacterController mover, Vector2Int destination, float secondsPerStep, MoveSpend spend, MoveAttempt attempt = null)
    {
        return new MoveRequest
        {
            Mover = mover,
            Kind = MoveKind.Move,
            Destination = destination,
            SecondsPerStep = secondsPerStep,
            Label = "AoO",
            AllowConvertStandard = false,
            Spend = spend,
            Quirks = MoveQuirk.AiDefault,
            Attempt = attempt
        };
    }

    /// <summary>An NPC or compulsion withdraw to <paramref name="destination"/>, which pays its own action (<see cref="MoveSpend.ExecutorOwned"/>).</summary>
    public static MoveRequest ForAIWithdraw(CharacterController mover, Vector2Int destination, float secondsPerStep, MoveAttempt attempt = null)
    {
        MoveRequest r = ForAI(mover, destination, secondsPerStep, MoveSpend.ExecutorOwned, attempt);
        r.Kind = MoveKind.Withdraw;
        r.Label = "AoO (Withdraw)";
        return r;
    }

    /// <summary>The standard action is converted when the move action is gone (the compulsion controllers' moves).</summary>
    public MoveRequest ConvertingStandard()
    {
        AllowConvertStandard = true;
        return this;
    }

    /// <summary>The after-walk spend is made whatever became of the mover or the combat (the charmed, confused and
    /// suggested moves, and the catalog's own AI move: they pay whenever the move was not refused).</summary>
    public MoveRequest SpendingEvenIfDropped()
    {
        Quirks &= ~(MoveQuirk.AiNoSpendIfDropped | MoveQuirk.AiNoSpendIfCombatOver);
        return this;
    }

    /// <summary>The after-walk spend is made after the combat ended too (the summon routines check only the mover).</summary>
    public MoveRequest SpendingAfterCombatEnd()
    {
        Quirks &= ~MoveQuirk.AiNoSpendIfCombatOver;
        return this;
    }

    /// <summary>A move that found no path is not spent (the catalog's own AI move pays only a move that started).</summary>
    public MoveRequest SpendingOnlyIfStarted()
    {
        Quirks &= ~MoveQuirk.AiSpendsWithoutPath;
        return this;
    }
}
