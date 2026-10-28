/// <summary>
/// The compulsions a creature is under at the start of a turn it acts in, evaluated once per turn for every actor, PC
/// or NPC, by <c>GameManager.TryRunCompulsionTurn</c> (called from <c>GameManager.BeginActingTurn</c>; batch 12 step 7,
/// decision D4). Each field is the decision of the shared controller for that condition: the confusion d% is rolled
/// here once, and the AI branches (AIService.ExecuteNPCTurnBody) and the PC forced turn read the stored result instead
/// of rolling again. Charmed is not a compulsion here: it stays a target restriction (the AI filters its targets; the
/// PC menu half is batch 13).
/// </summary>
public sealed class TurnCompulsion
{
    /// <summary>The confusion roll of this turn (PHB p.212), or null when the creature is not confused.</summary>
    public ConfusedBehaviorController.ConfusedTurnDecision Confused;

    /// <summary>The suggestion's course (the bard's Suggestion, PHB p.29 and p.285), or null.</summary>
    public SuggestedBehaviorController.SuggestedTurnDecision Suggested;

    /// <summary>The flight from a Frightened or Panicked condition's source (PHB p.310, p.311), or null.</summary>
    public FrightenedBehaviorController.FrightenedTurnDecision Fear;

    /// <summary>Turned (PHB p.159): the creature flees from the turner, or cowers when it cannot flee.</summary>
    public bool Turned;

    /// <summary>True when the confusion roll takes the turn over (any result but "act normally").</summary>
    public bool ConfusionForcesTurn
        => Confused != null && Confused.Mode != ConfusedBehaviorController.ConfusedTurnMode.ActNormally;

    /// <summary>True when any compulsion takes the turn over: confusion, a suggestion, fear or turning.</summary>
    public bool ForcesTurn => ConfusionForcesTurn || Suggested != null || Fear != null || Turned;
}

/// <summary>
/// What a creature's turn start decided, kept on <c>CharacterController.CurrentTurnStart</c> for the rest of that turn
/// (batch 12 step 7). Cleared at every turn start (<c>GameManager.BeginCombatantTurn</c>) and when a slot is reused
/// (<c>CharacterController.ResetForNewCreature</c>, CRE-046).
/// </summary>
public sealed class TurnStartState
{
    /// <summary>The compulsions of this turn, or null on a turn the creature does not act in (or before its first).</summary>
    public TurnCompulsion Compulsion;

    public void Clear()
    {
        Compulsion = null;
    }
}
