using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // ── The AI cores the Delegating executors wrap (batch 13 step 6). The public *ForAI wrappers route through
    // RunAIAction, so the executors reach the cores through these forwarders, never through the wrappers. ──

    /// <summary>The NPC attack with its whole prelude and post-sequence steps (NPCPerformAttack; the attack entries' AI path).</summary>
    internal IEnumerator ActionCore_NpcAttack(CharacterController npc, CharacterController target,
        Func<CharacterController, CharacterController, bool> tryStepManeuver, int maxSteps)
        => NPCPerformAttack(npc, target, tryStepManeuver, maxSteps);

    /// <summary>The shared 5-foot step (ExecuteFiveFootStep, no UI follow-up). False when the square is off the grid or the step failed (logged).</summary>
    internal bool ActionCore_FiveFootStep(CharacterController actor, Vector2Int destination)
    {
        if (actor == null || Grid == null)
            return false;
        SquareCell cell = Grid.GetCell(destination);
        return cell != null && ExecuteFiveFootStep(actor, cell, returnToActionChoices: false);
    }

    /// <summary>The breath weapon aimed at <paramref name="aimTarget"/> (the core overload of NPCExecuteBreathWeaponForAI; MM p.69, p.306).</summary>
    internal IEnumerator ActionCore_BreathWeapon(CharacterController actor, CharacterController aimTarget)
        => NPCExecuteBreathWeaponForAI(actor, aimTarget, catalogCore: true);

    /// <summary>A ranged special attack or spittle (AIService.ExecuteRangedSpecialAttackCore; MM p.315).</summary>
    internal bool ActionCore_RangedSpecial(CharacterController actor, CharacterController target)
        => _aiService != null && _aiService.ExecuteRangedSpecialAttackCore(actor, target);

    /// <summary>An engulf (AIService.ExecuteEngulfCore; MM p.201).</summary>
    internal bool ActionCore_Engulf(CharacterController actor, CharacterController target)
        => _aiService != null && _aiService.ExecuteEngulfCore(actor, target);

    /// <summary>Whether <paramref name="actor"/> is charmed by <paramref name="candidate"/> (the AI's charm link, PHB p.209; the legal set's charm filter).</summary>
    internal bool ActionCore_IsCharmedBy(CharacterController actor, CharacterController candidate)
        => _aiService != null && _aiService.IsCharmedByForActions(actor, candidate);
}
