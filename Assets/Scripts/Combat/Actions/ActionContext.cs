using System;
using UnityEngine;

/// <summary>
/// One request to take a catalog action (batch 13 step 3): who acts, whose choices these are, and what it is aimed at.
/// <see cref="CoreRequest"/> carries the request of the core a Delegating executor wraps when the caller built it
/// (AttackSequenceRequest, SpellCastRequest, ManeuverRequest, ChargeRequest, OverrunActionRequest); without one the
/// executor builds the core's request from this context and <see cref="Source"/>.
/// </summary>
public sealed class ActionContext
{
    public ActionDefinition Definition;
    public CharacterController Actor;

    /// <summary>Whose choices these are: the player (Ui) or the AI (an NPC, a summon, the harness, a forced PC turn; C7).</summary>
    public ActorSource Source;

    /// <summary>The runner fills it from <see cref="Source"/> (ActionDeciders.For).</summary>
    public IActionDecider Decider;

    public CharacterController Target;
    public Vector2Int? Cell;
    public ItemData Item;
    public int InventoryIndex = -1;
    public SpellData Spell;

    /// <summary>The entry's sub-choice: a wild shape form id or "revert", a bardic music ability or "concentrate"/"stop"/"suggestion".</summary>
    public string Variant;

    /// <summary>
    /// The wrapped core's own request, when the caller built it. An attack entry's AttackSequenceRequest means the caller
    /// already ran the attack prelude (the direct-attack gate, the ward break); the executor then runs only the sequence.
    /// </summary>
    public object CoreRequest;

    /// <summary>The AI's maneuver hook on an attack entry: it may replace a melee attack step with a maneuver (CMB-102).</summary>
    public Func<CharacterController, CharacterController, bool> TryStepManeuver;

    /// <summary>
    /// The caller reads the outcome at once and does not wait for a defender's prompt to settle (the NPC maneuver path,
    /// GameManager.TryNPCSpecialAttackIfBeneficial, which a scripted step or the AI coroutine waits on itself): the
    /// maneuver executor returns as soon as the core returns. Used by GameManager.RunAIActionNow callers (batch 13 step 6).
    /// </summary>
    public bool NoWait;

    public static ActionContext For(string id, CharacterController actor, ActorSource source, CharacterController target = null)
    {
        return new ActionContext
        {
            Definition = ActionCatalog.Get(id),
            Actor = actor,
            Source = source,
            Target = target,
        };
    }
}

/// <summary>What an action did (batch 13 step 3), for the runner's UI follow-up and the AI routine that goes on.</summary>
public sealed class ActionOutcome
{
    /// <summary>The action was taken (its cost paid); a refusal or a cancel leaves it false.</summary>
    public bool Performed;

    /// <summary>Why it was not taken (Ok when it was, or when nothing refused).</summary>
    public Legality Refusal = Legality.Allowed();

    /// <summary>The player backed out (the attack-of-opportunity prompt, GameManager.ConfirmActionProvocation; a cancelled cast).</summary>
    public bool Cancelled;

    /// <summary>The wrapped core already logged why the action was not taken, so the PC runner logs no second line.</summary>
    public bool RefusalLogged;

    /// <summary>
    /// The wrapped core continues the PC UI itself (the UI cast decider's finish continuation), so the PC runner adds no
    /// follow-up, whatever the entry's UiAfter.
    /// </summary>
    public bool CoreOwnsUi;

    /// <summary>
    /// The largest action spent by the run (the runner reads the action economy before and after): Free when the executor
    /// ran and spent nothing; it stays NotAnAction when the run was refused before the executor ran.
    /// </summary>
    public ActionCostType Spent = ActionCostType.NotAnAction;

    /// <summary>The action provoked attacks of opportunity that were taken.</summary>
    public bool Provoked;

    /// <summary>The actor ended out of the fight (dead, dying, unconscious).</summary>
    public bool ActorDown;

    public bool CombatEnded;

    /// <summary>The wrapped core's own outcome (AttackSequenceOutcome, CastOutcome, ManeuverOutcome, ChargeOutcome, OverrunMoveOutcome, MoveAttempt).</summary>
    public object CoreOutcome;

    /// <summary>Marks the action refused with <paramref name="refusal"/> (nothing performed).</summary>
    public void Refuse(Legality refusal)
    {
        Performed = false;
        Refusal = refusal.Ok ? Legality.Refused(LegalityBar.Other, "refused") : refusal;
    }

    public override string ToString()
        => Performed ? $"performed({Spent})" : (Cancelled ? "cancelled" : Refusal.ToString());
}

/// <summary>
/// The AI move request of the move and withdraw entries (batch 13 step 6): the walk's pace, the core's MoveAttempt the
/// caller reads, and whether the caller pays the move action itself (every AI move site pays after the walk as it does
/// today, AI-076, so its routing through the catalog changes nothing; batch 13 steps 9-10 make one payment rule).
/// </summary>
public sealed class AiMoveRequest
{
    public float SecondsPerStep;
    public MoveAttempt Attempt;
    public bool CallerPays;
}

/// <summary>
/// The AI attack request of the attack and full attack entries (batch 13 step 6): the cap on the sequence's steps of
/// GameManager.NPCPerformAttack (1: the attack action alone; int.MaxValue: every step the action economy allows), as the
/// caller asked it through GameManager.NPCPerformAttackForAI.
/// </summary>
public sealed class NpcAttackRequest
{
    public int MaxSteps = int.MaxValue;
}
