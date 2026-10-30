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

    /// <summary>
    /// The creatures a fear or a turning makes this one flee from (the fear's source, the turner; FlightRules.Sources),
    /// set at the turn start for every actor under a fear or a turning (batch 13 step 20, D15). Empty otherwise.
    /// </summary>
    public System.Collections.Generic.List<CharacterController> FlightSources = new System.Collections.Generic.List<CharacterController>();

    /// <summary>
    /// The creature had no flight option at the start of its turn (FlightRules.HasFlightOption against every source at once:
    /// no move, double move, withdraw, stand-and-move or crawl ending farther from them all, no grapple to escape, no
    /// castable Escape spell; D15): it is cornered. A cornered frightened (not panicked) creature may fight (PHB p.309), so
    /// the legal set's compulsion filter lets it act; a cornered panicked or turned one cowers (PHB p.311, p.159). A run is
    /// not counted until the Run action exists (CMB-202), so a PC that can get away only by running is cornered here and
    /// its forced flight (GameManager.ExecuteFleeingForcedPCTurn) runs for it. Set for every actor, PC or NPC.
    /// </summary>
    public bool Cornered;

    /// <summary>
    /// The turn's flight is chosen from the restricted menu (D15, inherited owner decision Q1; batch 13 step 20): set only
    /// by GameManager.TryRunCompulsionTurn when the IsControllable fork picks the menu over the forced flight, for a
    /// frightened (not panicked) or turned creature that is not cornered and not grappling. While it is set, every move
    /// must end farther from every flight source (FlightRules.DestinationRefusal) and End Turn is refused while a flight
    /// option is left (FlightRules.EndTurnRefusal).
    /// </summary>
    public bool ChoosesFlight;

    /// <summary>True when a fear (frightened or panicked) or a turning compels the creature to flee this turn.</summary>
    public bool CompelsFlight => Fear != null || Turned;

    /// <summary>True when the confusion roll takes the turn over (any result but "act normally").</summary>
    public bool ConfusionForcesTurn
        => Confused != null && Confused.Mode != ConfusedBehaviorController.ConfusedTurnMode.ActNormally;

    /// <summary>True when any compulsion takes the turn over: confusion, a suggestion, fear or turning (a chosen flight, D15, still flees).</summary>
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

    /// <summary>
    /// The creature is panicked and cornered this turn, so it cowers, typically using total defense (PHB p.311; D2): the
    /// one case a panicked creature may take total defense (TotalDefenseRules.Check). Set only by
    /// FrightenedBehaviorController.ExecuteDecision when the flight finds no way out.
    /// </summary>
    public bool CorneredInPanic;

    public void Clear()
    {
        Compulsion = null;
        CorneredInPanic = false;
    }
}
