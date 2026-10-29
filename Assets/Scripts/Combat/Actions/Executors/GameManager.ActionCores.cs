using System.Collections;
using System.Collections.Generic;

/// <summary>
/// GameManager partial class: thin forwarders from the catalog's Delegating executors to the private cores they wrap
/// (batch 13 step 3). No new rule and no UI follow-up here; ActionCatalogTests scans this file with the executors. A
/// forwarder exists only where no public *ForAI wrapper of the same core does (TryNPCDeliverHeldChargeForAI is one).
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// Stands <paramref name="actor"/> up through the shared stand-up core (ResolveStandUpFromProne: the move action, then
    /// the AoOs, PHB p.143 and Table 8-2), after the gate both live callers apply first: a creature that is not prone, or
    /// may not stand (GetStandUpDisabledReason: pinned, grappled, no move action left), is refused with nothing spent,
    /// the AI's refusal logged as TryStandUpFromProneForAI logs it. For the player (<paramref name="source"/> Ui) the
    /// threatening foes are confirmed first through <paramref name="decider"/> (the StandFromProne prompt of
    /// OnStandUpButtonPressed), and the AoO waits are the PC's 0.8 s, else the NPC's 0.5 s. Performed only when the move
    /// action was spent (an AoO can still keep the creature down).
    /// </summary>
    internal IEnumerator ActionCore_StandUp(CharacterController actor, ActorSource source, IActionDecider decider, ActionOutcome o)
    {
        if (actor == null || actor.Stats == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no actor"));
            yield break;
        }
        if (!actor.HasCondition(CombatConditionType.Prone))
        {
            o.Refuse(Legality.Refused(LegalityBar.Condition, actor.Stats.CharacterName + " is not prone"));
            yield break;
        }

        string reason = GetStandUpDisabledReason(actor);
        if (!string.IsNullOrEmpty(reason))
        {
            o.Refuse(Legality.Refused(LegalityBar.Condition, reason));
            if (source == ActorSource.AI)
            {
                LogStaysProne(actor, reason);
                o.RefusalLogged = true;
            }
            yield break;
        }

        List<CharacterController> threateners = null;
        if (source == ActorSource.Ui)
        {
            threateners = ThreatSystem.GetStandUpAoOProvokers(actor, GetAllCharacters());
            if (threateners.Count > 0)
            {
                yield return ConfirmActionProvocation(actor, AoOProvokingAction.StandFromProne, "Stand up", threateners, decider, o);
                if (o.Cancelled || o.CombatEnded || !o.Refusal.Ok)
                    yield break;
            }
            CurrentSubPhase = PlayerSubPhase.Animating;
        }

        ActionCosts.Snapshot before = ActionCosts.Take(actor);
        var standUp = new StandUpOutcome();
        yield return ResolveStandUpFromProne(actor, threateners, source == ActorSource.Ui ? 0.8f : 0.5f, standUp);
        o.Performed = ActionCosts.SpentSince(before, actor) != ActionCostType.Free;
        o.ActorDown = standUp.Incapacitated;
        o.CoreOutcome = standUp.StoodUp;
    }
}
