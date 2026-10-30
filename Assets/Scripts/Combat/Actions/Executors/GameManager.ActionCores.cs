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

    // ── Posture and reload (batch 13 step 12, D4, D5): the Extracted executors' cores, moved from the PC-bound button
    // code (the old PC-only ExecuteReload and ExecuteCrawl and OnDropProneButtonPressed's body, removed with step 12). No UI follow-up here: the PC
    // runner's UiAfter does it. ──

    /// <summary>
    /// Reloads <paramref name="actor"/>'s main weapon (D4, ReloadRules): refused with nothing spent when it may not
    /// reload now; else, once <paramref name="decider"/> goes on (the UI's attack-of-opportunity prompt when foes
    /// threaten; a cancel spends nothing), the load's action is paid (free, move or full-round, by the weapon and Rapid
    /// Reload), every threatening foe takes its attack of opportunity (ResolveActionProvocation, LoadCrossbow; PHB p.115,
    /// p.99), and the crossbow is loaded unless an attack dropped the actor or took the weapon. Performed when it loaded.
    /// </summary>
    internal IEnumerator ActionCore_Reload(CharacterController actor, ActorSource source, IActionDecider decider, ActionOutcome o)
    {
        if (actor == null || actor.Stats == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no actor"));
            yield break;
        }
        ItemData weapon = actor.GetEquippedMainWeapon();
        if (!ReloadRules.Check(actor, out string reason, out ReloadActionType reloadAction, out LegalityBar bar))
        {
            o.Refuse(Legality.Refused(bar, reason));
            yield break;
        }

        ActionCostType cost = ReloadRules.CostOf(reloadAction);
        bool PayLoad()
        {
            if (source == ActorSource.Ui)
                CurrentSubPhase = PlayerSubPhase.Animating;
            return ActionCosts.TryPay(actor, cost);
        }
        yield return ResolveActionProvocation(actor, AoOProvokingAction.LoadCrossbow, decider, o, "reload", PayLoad);
        if (o.Cancelled || o.CombatEnded || !o.Refusal.Ok)
            yield break;

        if (CombatEndRules.IsOutOfFight(actor))
        {
            o.ActorDown = true;
            yield break;
        }
        if (weapon == null || actor.GetEquippedMainWeapon() != weapon || weapon.IsLoaded || !actor.ReloadWeapon(weapon))
        {
            string name = weapon != null ? weapon.Name : "the weapon";
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{name} could not be reloaded."));
            o.Refuse(Legality.Refused(LegalityBar.Equipment, name + " could not be reloaded"));
            o.RefusalLogged = true;
            yield break;
        }

        string actionLabel = CharacterController.GetReloadActionLabel(reloadAction);
        CombatUI?.ShowCombatLog($"🔄 {actor.Stats.CharacterName} reloads {weapon.Name} ({actionLabel} action).");
        UpdateAllStatsUI();
        o.Performed = true;
    }

    /// <summary>Whether <paramref name="actor"/> can crawl into <paramref name="destination"/> (the crawl executor's CanExecute; IsValidCrawlDestination).</summary>
    internal bool IsValidCrawlDestinationForActions(CharacterController actor, Vector2Int destination)
        => IsValidCrawlDestination(actor, destination);

    /// <summary>
    /// Crawls <paramref name="actor"/> 5 ft into <paramref name="destination"/> (D5; PHB p.142-143). A barrier across the
    /// step refuses the crawl before it starts, with nothing spent (D3). Otherwise the move action is spent when the crawl
    /// starts (D3) and the crawl provokes from every foe that threatens the creature at any point of it (PHB p.143): each
    /// foe that threatens the square left takes its attack of opportunity before the creature leaves it (the shared
    /// movement AoO step, PHB p.137, CMB-005), then the one-square move (MoveKind.Crawl: the step hooks read it), then each
    /// foe that threatens only the square entered takes its attack there (ThreatSystem.CrawlArrivalThreateners; one per
    /// foe, PHB p.138). A crawl a before-step hook stops (a hold, Grease's failed Balance check, a real attempt) keeps its
    /// spent action, provokes nothing and logs that it stopped. An attack that drops the actor ends it
    /// (<see cref="ActionOutcome.ActorDown"/>), one that only stops it leaves it in place. The AoO waits are the PC's
    /// 0.8 s, else 0.5 s.
    /// </summary>
    internal IEnumerator ActionCore_Crawl(CharacterController actor, Vector2Int destination, ActorSource source, ActionOutcome o)
    {
        if (actor == null || actor.Stats == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no actor"));
            yield break;
        }
        if (!IsValidCrawlDestination(actor, destination))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Invalid crawl destination."));
            o.Refuse(Legality.Refused(LegalityBar.Terrain, "invalid crawl destination"));
            o.RefusalLogged = true;
            yield break;
        }
        // A barrier across the step refuses the crawl before it starts: nothing is spent and nothing provoked (D3, PHB p.137).
        if (SquareGridUtils.IsStepBlockedByBarrier(actor, actor.GridPosition, destination))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{actor.Stats.CharacterName} cannot crawl there: a barrier blocks the way."));
            o.Refuse(Legality.Refused(LegalityBar.Terrain, "a barrier blocks the crawl"));
            o.RefusalLogged = true;
            yield break;
        }

        if (source == ActorSource.Ui)
            CurrentSubPhase = PlayerSubPhase.Animating;
        float aooWait = source == ActorSource.Ui ? 0.8f : 0.5f;

        var crawlPath = new List<Vector2Int> { destination };
        var provokedAoOs = _movementService != null
            ? _movementService.CheckForAoO(actor, crawlPath)
            : ThreatSystem.AnalyzePathForAoOs(actor, crawlPath, GetAllCharacters());

        Vector2Int oldPos = actor.GridPosition;
        ConsumeMoveAction(actor);
        o.Performed = true;

        // A crawl a before-step hook stops (a hold, Grease's failed Balance check) provokes nothing (PHB p.137); its
        // action stays spent, as a move a hook stops after it started (D3).
        if (MovementService.IsStepRefused(actor, actor.GridPosition, destination, MoveKind.Crawl))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⛔", $"{actor.Stats.CharacterName}'s crawl is stopped before it leaves its square."));
            RefreshFlankedConditions();
            UpdateAllStatsUI();
            InvalidatePreviewThreats();
            yield break;
        }

        // The foes that threaten the square left: their AoOs resolve before the creature leaves it (PHB p.137, p.143, CMB-005).
        if (provokedAoOs.Count > 0)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Crawling provokes attacks of opportunity!"));

        var aooOutcome = new MovementAoOStepOutcome();
        o.Provoked = provokedAoOs.Count > 0;
        yield return StartCoroutine(ResolveMovementAoOsBeforeStep(actor, provokedAoOs, 0, "AoO (crawling)", aooWait, aooOutcome));

        if (!aooOutcome.StopMovement)
        {
            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(actor, crawlPath, PlayerMoveSecondsPerStep, markAsMoved: true, kind: MoveKind.Crawl));
            else
                yield return StartCoroutine(actor.MoveAlongPath(crawlPath, PlayerMoveSecondsPerStep, markAsMoved: true, kind: MoveKind.Crawl));
        }

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();

        if (aooOutcome.Incapacitated)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔", $"{actor.Stats.CharacterName}'s crawl is interrupted by incapacitation."));
            o.ActorDown = true;
            yield break;
        }

        if (aooOutcome.StopMovement || actor.GridPosition != destination)
        {
            // Stopped but still able to act (e.g. an AoO left it unable to move, or a step hook stopped it).
            string why = aooOutcome.StopMovement ? aooOutcome.Reason + ") after an attack of opportunity" : "a step rule stopped it)";
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⛔", $"{actor.Stats.CharacterName}'s crawl stops ({why}."));
            yield break;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{actor.Stats.CharacterName} crawls ({oldPos.x},{oldPos.y} → {destination.x},{destination.y})."));

        // The foes that threaten only the square entered: the crawl provokes from every attacker that threatens the creature
        // at any point of it (PHB p.143), so each takes its attack there, once (PHB p.138).
        List<AoOThreatInfo> arrivalAoOs = ThreatSystem.CrawlArrivalThreateners(actor, GetAllCharacters());
        if (arrivalAoOs.Count == 0)
            yield break;
        if (provokedAoOs.Count == 0)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Crawling provokes attacks of opportunity!"));
        o.Provoked = true;
        var arrivalOutcome = new MovementAoOStepOutcome();
        yield return StartCoroutine(ResolveMovementAoOsBeforeStep(actor, arrivalAoOs, 0, "AoO (crawling)", aooWait, arrivalOutcome));
        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
        if (arrivalOutcome.Incapacitated || CombatEndRules.IsOutOfFight(actor))
            o.ActorDown = true;
    }

    /// <summary>
    /// Drops <paramref name="actor"/> prone (D5; PHB p.141 Table 8-2: a free action, no attacks of opportunity). False,
    /// nothing changed, when it may not (GetDropProneDisabledReason: already prone, pinned, grappled).
    /// </summary>
    internal bool ActionCore_DropProne(CharacterController actor, ActionOutcome o)
    {
        string reason = actor != null ? GetDropProneDisabledReason(actor) : "No active character";
        if (!string.IsNullOrEmpty(reason))
        {
            o.Refuse(Legality.Refused(LegalityBar.Condition, reason));
            return false;
        }

        actor.ApplyCondition(CombatConditionType.Prone, -1, actor.Stats.CharacterName);
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{actor.Stats.CharacterName} drops prone."));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "(Free action - no attacks of opportunity provoked)"));

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
        return true;
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
