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
        // A chosen flight's crawl ends farther from every source and cuts no fuller flight short (standing up and moving;
        // D15, batch 13 step 20): nothing is spent.
        string flightRefusal = FlightRules.DestinationRefusal(actor, destination, shortMove: true);
        if (flightRefusal != null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", flightRefusal + "."));
            o.Refuse(Legality.Refused(LegalityBar.Compulsion, flightRefusal));
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

    // ── Aid another, waking an ally and attacking a wall (batch 13 step 13, D6, CORE-040): the Extracted executors'
    // cores, moved from the PC-bound ExecuteAidAnother, ExecuteAidAnotherWakeSleepingAlly, PerformPlayerAttackOnWall and
    // PerformStrengthCheckOnWall. Each pays its standard action first, then rolls. No UI follow-up here: the PC runner's
    // UiAfter does it. ──

    /// <summary>
    /// Why <paramref name="actor"/> cannot aid another's attack or AC now, or null (the aid-another entry's CanStart): a
    /// standard action left (PHB p.154), not grappling or pinned (a grappler takes only the grapple actions, PHB p.156), an
    /// ally on the field and a foe it threatens with its weapon (the "in position to make a melee attack" reading of
    /// CanAttemptAidAnotherTouch, delegated RAW reading 2026-10-29). Pure, unlogged.
    /// </summary>
    internal string GetAidAnotherDisabledReason(CharacterController actor)
    {
        if (actor == null || actor.Stats == null || actor.Actions == null)
            return "no actor";
        if (!actor.Actions.HasStandardAction)
            return "no standard action left";
        string grappled = GetSupportActionGrappleRefusal(actor);
        if (grappled != null)
            return grappled;
        if (GetAllAllies(actor).Count == 0)
            return "no ally";
        ItemData weapon = actor.GetEquippedWeapon();
        foreach (CharacterController enemy in GetAllEnemies(actor))
            if (actor.ThreatensWith(enemy, weapon))
                return null;
        return "threatens no foe";
    }

    /// <summary>
    /// The grapple bar of the support and wall entries (aid another, waking an ally, attacking a wall), or null: a pinned
    /// creature can take no action but the grapple's escape, and a grappling one only the grapple actions (PHB p.156), none
    /// of which these are. Its reasons map to <see cref="LegalityBar.Condition"/> (<see cref="IsGrappleBarReason"/>).
    /// </summary>
    internal static string GetSupportActionGrappleRefusal(CharacterController actor)
    {
        if (actor == null)
            return null;
        if (actor.HasCondition(CombatConditionType.Pinned))
            return "pinned";
        if (actor.IsGrappling())
            return "grappling";
        return null;
    }

    /// <summary>Whether <paramref name="reason"/> is the grapple bar's (<see cref="GetSupportActionGrappleRefusal"/>).</summary>
    internal static bool IsGrappleBarReason(string reason) => reason == "pinned" || reason == "grappling";

    /// <summary>
    /// Why <paramref name="aider"/> cannot aid <paramref name="ally"/> against <paramref name="opponent"/>, or null (the
    /// aid-another entry's CanExecute after CanStart): the aider can make a melee attack on the opponent (it threatens it
    /// with its weapon) and the ally, another living creature of its side, is in melee with it (threatens it; PHB p.154).
    /// Delegated RAW reading (2026-10-29, RULES_COVERAGE 4.1): PHB p.154's "in position to make a melee attack" on the
    /// opponent is read as threatening it, since PHB p.137 defines the threatened squares as those into which a creature
    /// can make a melee attack and says an unarmed creature does not normally threaten; so an aider holding only a bow, or
    /// unarmed without Improved Unarmed Strike, cannot aid.
    /// </summary>
    internal string GetAidAnotherTargetRefusal(CharacterController aider, CharacterController ally, CharacterController opponent)
    {
        if (opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
            return "no opponent to aid against";
        if (!CanAttemptAidAnotherTouch(aider, opponent))
            return GetCombatantName(opponent) + " is not in melee reach for Aid Another";
        if (ally == null || ally.Stats == null || ally.Stats.IsDead || ally == aider || !TeamUtility.IsAlly(aider, ally))
            return "no ally to aid";
        if (!ally.ThreatensWith(opponent, ally.GetEquippedWeapon()))
            return GetCombatantName(ally) + " is not in melee range of " + GetCombatantName(opponent);
        return null;
    }

    /// <summary>
    /// <paramref name="aider"/> aids <paramref name="ally"/>'s next attack roll or AC against <paramref name="opponent"/>
    /// (D6; PHB p.154): the standard action is paid first, then an attack roll against AC 10 with the aider's melee attack
    /// modifier (GetAidAnotherAttackModifier: the shared BuildAttackBonus terms of its melee weapon, or unarmed, plus the
    /// weapon's enhancement or masterwork bonus); a hit gives the ally +2 (AddAidBonus, until it is used or the start of
    /// the aider's next turn). Neither aid provokes (Table 8-2 "Maybe", note 2: it provokes only when the aided action would, and
    /// an attack roll or AC aids no action that provokes now). The caller checked the targets (GetAidAnotherTargetRefusal).
    /// Performed when the action was paid, hit or miss; <see cref="ActionOutcome.CoreOutcome"/> is whether it hit.
    /// </summary>
    internal bool ActionCore_AidAnother(CharacterController aider, CharacterController ally, CharacterController opponent, AidType aidType,
        ActorSource source, ActionOutcome o)
    {
        if (aider == null || aider.Stats == null || ally == null || ally.Stats == null || opponent == null || opponent.Stats == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no aider, ally or opponent"));
            return false;
        }
        // D6: the action is paid when it is taken, before its roll.
        if (!aider.CommitStandardAction())
        {
            o.Refuse(Legality.Refused(LegalityBar.Action, "no standard action left"));
            return false;
        }
        o.Performed = true;
        if (source == ActorSource.Ui)
            CurrentSubPhase = PlayerSubPhase.Animating;

        CombatUI?.ShowCombatLog(CombatLogHelper.Success("🤝", $"{aider.Stats.CharacterName} aids {ally.Stats.CharacterName}'s {GetAidTypeLabel(aidType)} vs {opponent.Stats.CharacterName}."));

        int aidAtkMod = GetAidAnotherAttackModifier(aider);
        (bool hit, int roll, int total) = aider.Stats.RollToHitWithMod(aidAtkMod, 10);

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Attack roll vs AC 10:"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  Roll: 1d20 = {roll}"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  Modifier: {CharacterStats.FormatMod(aidAtkMod)}"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  Total: {total}"));
        o.CoreOutcome = hit;

        if (!hit)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", "Aid Another failed (needed 10)."));
            UpdateAllStatsUI();
            return true;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Success("✅", "Aid Another success!"));
        AddAidBonus(aider, ally, opponent, aidType);
        DisplayActiveAidBonuses(ally);
        UpdateAllStatsUI();
        return true;
    }

    /// <summary>
    /// Why <paramref name="actor"/> cannot shake a sleeping ally awake now, or null (the wake-ally entry's CanStart): a
    /// standard action left (PHB p.280: waking a sleeper is a standard action, an application of aid another, D6), not
    /// grappling or pinned (PHB p.156), and a sleeping ally adjacent to it. Pure, unlogged.
    /// </summary>
    internal string GetWakeAllyDisabledReason(CharacterController actor)
    {
        if (actor == null || actor.Stats == null || actor.Actions == null)
            return "no actor";
        if (!actor.Actions.HasStandardAction)
            return "no standard action left";
        string grappled = GetSupportActionGrappleRefusal(actor);
        if (grappled != null)
            return grappled;
        if (GetAdjacentSleepingAllies(actor).Count == 0)
            return "no sleeping ally adjacent";
        return null;
    }

    /// <summary>Why <paramref name="actor"/> cannot wake <paramref name="ally"/>, or null: an ally, adjacent, asleep.</summary>
    internal string GetWakeAllyTargetRefusal(CharacterController actor, CharacterController ally)
    {
        if (ally == null || ally.Stats == null || ally.Stats.IsDead || ally == actor || !TeamUtility.IsAlly(actor, ally))
            return "no ally to wake";
        if (!IsAdjacent(actor, ally))
            return ally.Stats.CharacterName + " is not adjacent to " + actor.Stats.CharacterName;
        if (!IsCharacterAsleep(ally))
            return ally.Stats.CharacterName + " is not asleep";
        return null;
    }

    /// <summary>
    /// <paramref name="actor"/> shakes <paramref name="ally"/> awake (D6; PHB p.280, p.154): the standard action is paid,
    /// then the ally wakes (TryWakeSleepingCharacter). It provokes nothing. Performed when the action was paid;
    /// <see cref="ActionOutcome.CoreOutcome"/> is whether the ally woke.
    /// </summary>
    internal bool ActionCore_WakeAlly(CharacterController actor, CharacterController ally, ActorSource source, ActionOutcome o)
    {
        if (actor == null || actor.Stats == null || ally == null || ally.Stats == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no actor or ally"));
            return false;
        }
        if (!actor.CommitStandardAction())
        {
            o.Refuse(Legality.Refused(LegalityBar.Action, "no standard action left"));
            return false;
        }
        o.Performed = true;
        if (source == ActorSource.Ui)
            CurrentSubPhase = PlayerSubPhase.Animating;

        bool woke = TryWakeSleepingCharacter(ally, "shaken awake", actor, suppressLog: true);
        if (woke)
            CombatUI?.ShowCombatLog(CombatLogHelper.Success("🤝", $"{actor.Stats.CharacterName} shakes {ally.Stats.CharacterName} awake."));
        else
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{actor.Stats.CharacterName} cannot wake {ally.Stats.CharacterName} right now."));
        o.CoreOutcome = woke;
        UpdateAllStatsUI();
        return true;
    }

    /// <summary>
    /// The attack intent of a wall attack by <paramref name="actor"/> (batch 13 step 13 review): the PC UI's attack selection
    /// for a Ui run (AttackIntent.ForUi: a throw only when the player chose it), else the AI's mode with no creature target
    /// (AttackIntent.ForAI: a ranged weapon shoots, any other weapon strikes in melee).
    /// </summary>
    internal AttackIntent WallAttackIntent(CharacterController actor, ActorSource source)
        => source == ActorSource.Ui ? AttackIntent.ForUi(this, actor) : AttackIntent.ForAI(this, actor, null);

    /// <summary>The distance in squares (Chebyshev) from the nearest square of <paramref name="actor"/>'s space to <paramref name="cell"/>, so a Large creature measures from its whole footprint.</summary>
    internal static int WallCellDistance(CharacterController actor, Vector2Int cell)
    {
        int best = int.MaxValue;
        List<Vector2Int> squares = actor.GetOccupiedSquares();
        if (squares != null)
            foreach (Vector2Int sq in squares)
                best = Mathf.Min(best, SquareGridUtils.ChebyshevDistance(sq, cell));
        return best == int.MaxValue ? SquareGridUtils.ChebyshevDistance(actor.GridPosition, cell) : best;
    }

    /// <summary>
    /// Whether a weapon attack by <paramref name="actor"/> with <paramref name="weapon"/> (null: unarmed or natural) and
    /// <paramref name="intent"/> reaches the wall square <paramref name="cell"/>: a ranged attack (a ranged weapon, or a
    /// declared throw) within the weapon's maximum range (RangeCalculator.GetMaxRangeSquares: ten increments, five thrown;
    /// PHB p.113-114), a melee one within the weapon's reach band (GetMeleeMinAttackDistance to GetMeleeMaxAttackDistance:
    /// a reach weapon cannot strike an adjacent square, PHB p.113). Any attack can target an object (PHB p.165-166), so this
    /// is the one square test of the Attack button's wall highlights (ShowAttackTargets) and of the attack-wall executor's
    /// weapon variant. Measured from the actor's whole space (<see cref="WallCellDistance"/>).
    /// </summary>
    internal bool IsWallCellInWeaponAttackReach(CharacterController actor, Vector2Int cell, ItemData weapon, AttackIntent intent)
    {
        if (actor == null || actor.Stats == null)
            return false;
        int dist = WallCellDistance(actor, cell);
        if (IsAttackModeRanged(actor, weapon, intent))
        {
            bool thrown = IsUsingThrownAttackMode(actor, weapon, intent)
                || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);
            int maxSquares = weapon != null && weapon.RangeIncrement > 0
                ? RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, thrown)
                : actor.Stats.AttackRange;
            return dist <= maxSquares;
        }
        return dist >= actor.GetMeleeMinAttackDistance(weapon) && dist <= actor.GetMeleeMaxAttackDistance(weapon);
    }

    /// <summary>
    /// Why <paramref name="actor"/> cannot shoot <paramref name="weapon"/> at a wall now, or null (pure, unlogged): the
    /// weapon is usable (CharacterController.CanAttackWithWeapon: a loaded crossbow) and a projectile weapon has a piece of
    /// ammunition (PHB p.113). The same rule as the attack sequence's stage 4 (CanShootWithWeapon), without its log line.
    /// </summary>
    internal string GetWallShotRefusal(CharacterController actor, ItemData weapon)
    {
        if (!actor.CanAttackWithWeapon(weapon, out string reason))
            return reason;
        if (weapon != null && weapon.IsProjectileWeapon)
        {
            Inventory inv = Combat_GetCharacterInventory(actor);
            if (inv != null && !inv.HasAmmo(weapon.RequiresAmmoType))
                return "no ammunition for " + weapon.Name;
        }
        return null;
    }

    /// <summary>
    /// Why <paramref name="actor"/> cannot attack or break a wall now, or null (the attack-wall entry's CanStart): not
    /// grappling or pinned (a grappler takes only the grapple actions, PHB p.156), a standard action left unless
    /// <paramref name="actionPaid"/> (both are a standard action: attacking an object, PHB p.165-166; the Strength check
    /// to break the wall, PHB p.299), and, with <paramref name="requireWall"/>, an intact Wall of Ice square it can break
    /// (adjacent) or strike with its weapon (<see cref="IsWallCellInWeaponAttackReach"/>, with the AI's attack intent).
    /// </summary>
    internal string GetAttackWallDisabledReason(CharacterController actor, bool actionPaid = false, bool requireWall = true)
    {
        if (actor == null || actor.Stats == null || actor.Actions == null)
            return "no actor";
        string grappled = GetSupportActionGrappleRefusal(actor);
        if (grappled != null)
            return grappled;
        if (!actionPaid && !actor.Actions.HasStandardAction)
            return "no standard action left";
        if (!requireWall)
            return null;
        ItemData weapon = actor.GetEquippedMainWeapon();
        AttackIntent intent = WallAttackIntent(actor, ActorSource.AI);
        foreach (Vector2Int cell in WallOfIceAreaEffect.GetAllIntactWallOfIceCells())
            if (WallCellDistance(actor, cell) <= 1 || IsWallCellInWeaponAttackReach(actor, cell, weapon, intent))
                return null;
        return "no intact Wall of Ice in reach";
    }

    /// <summary>
    /// Why <paramref name="actor"/> cannot attack the wall square <paramref name="cell"/> this way, or null: an intact Wall
    /// of Ice square; for the Strength check (<paramref name="byStrength"/>, PHB p.299) adjacent to the actor's space; for a
    /// weapon attack within the reach or range of the attack <paramref name="intent"/> describes
    /// (<see cref="IsWallCellInWeaponAttackReach"/>), and a ranged shot that can be made (<see cref="GetWallShotRefusal"/>).
    /// </summary>
    internal string GetAttackWallCellRefusal(CharacterController actor, Vector2Int cell, bool byStrength, AttackIntent intent)
    {
        WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell);
        if (wall == null)
            return "no Wall of Ice there";
        if (wall.IsBreached(cell))
            return "that wall square is already breached";
        if (byStrength)
            return WallCellDistance(actor, cell) <= 1 ? null : actor.Stats.CharacterName + " must be adjacent to the wall to break through it";

        ItemData weapon = actor.GetEquippedMainWeapon();
        bool ranged = IsAttackModeRanged(actor, weapon, intent);
        if (!IsWallCellInWeaponAttackReach(actor, cell, weapon, intent))
            return "that wall square is out of " + actor.Stats.CharacterName + "'s " + (ranged ? "range" : "reach");
        return ranged ? GetWallShotRefusal(actor, weapon) : null;
    }

    /// <summary>
    /// <paramref name="actor"/> attacks the Wall of Ice square <paramref name="cell"/> (CORE-040). The standard action is
    /// paid first unless <paramref name="actionPaid"/> (the PC's Attack button paid the attack step before the square was
    /// clicked, as it pays any attack). <paramref name="byStrength"/>: a Strength check to break through (DC 15 + caster
    /// level, PHB p.299; WallOfIceAreaEffect.AttemptStrengthCheck); else one weapon, natural or unarmed attack, which hits
    /// the stationary wall automatically (PHB p.165-166, p.299) and deals its damage
    /// (CharacterController.RollObjectAttackDamage; fire as the wall takes it) to that square. A ranged attack (a shot or a
    /// declared throw, by the attack intent) provokes from every foe that threatens the shooter (Table 8-2: Attack
    /// (ranged), PHB p.141; CombatFlowService.ResolveRangedAttackAoOIfProvoked, as every shot does), deals half damage to
    /// the object (PHB p.165, Ranged Weapon Damage), spends its piece of ammunition and leaves a crossbow unloaded. A thrown
    /// weapon stays in hand (CMB-208). For the player (<paramref name="source"/> Ui) the PC's attack flow is closed (the
    /// highlights, the break-wall selection, the attack sequence); the PC runner's UiAfter then does the follow-up. Nothing
    /// here ends a turn, so the NPC's turn coroutine stays the only one that ends its turn (CORE-012). Performed when the
    /// action was spent on the wall.
    /// </summary>
    internal bool ActionCore_AttackWall(CharacterController actor, Vector2Int cell, bool byStrength, bool actionPaid, ActorSource source, ActionOutcome o)
    {
        AttackIntent intent = actor != null && actor.Stats != null ? WallAttackIntent(actor, source) : null;
        string refusal = actor == null || actor.Stats == null ? "no actor" : GetAttackWallCellRefusal(actor, cell, byStrength, intent);
        if (refusal != null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, refusal));
            return false;
        }
        WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell);
        if (!actionPaid && !actor.CommitStandardAction())
        {
            o.Refuse(Legality.Refused(LegalityBar.Action, "no standard action left"));
            return false;
        }
        o.Performed = true;
        if (source == ActorSource.Ui)
            CurrentSubPhase = PlayerSubPhase.Animating;

        var sb = new System.Text.StringBuilder();
        if (byStrength)
        {
            bool success = wall.AttemptStrengthCheck(actor, cell);
            int strMod = CharacterStats.GetModifier(actor.Stats.STR);
            int dc = 15 + wall.CasterLevel;
            sb.AppendLine("═══════════════════════════════════");
            sb.AppendLine($"💪 {actor.Stats.CharacterName} attempts to break through Wall of Ice at ({cell.x},{cell.y})!");
            sb.AppendLine($"  Strength Check DC {dc} (15 + CL {wall.CasterLevel})");
            sb.AppendLine($"  STR modifier: {(strMod >= 0 ? "+" : "")}{strMod}");
            sb.AppendLine(success ? "  ✅ Success! The ice cracks open — cell breached!" : "  ❌ Failed! The wall holds firm.");
            sb.Append("═══════════════════════════════════");
            o.CoreOutcome = success;
        }
        else
        {
            ItemData weapon = actor.GetEquippedMainWeapon();
            bool isRanged = IsAttackModeRanged(actor, weapon, intent);

            // A ranged attack provokes from every foe threatening the shooter (PHB p.141 Table 8-2), before it is made.
            if (isRanged && _combatFlowService != null && !_combatFlowService.ResolveRangedAttackAoOIfProvoked(actor, null, intent))
            {
                o.ActorDown = CombatEndRules.IsOutOfFight(actor);
                o.CombatEnded = CurrentPhase == TurnPhase.CombatOver;
                UpdateAllStatsUI();
                FinishWallAttackUi(source);
                return true;
            }

            // The wall is a stationary object: the attack hits automatically.
            int damage = actor.RollObjectAttackDamage(weapon, isRanged, out string weaponName, out string damageRoll);

            // Fire damage from a weapon's spell effect (fire is especially effective against ice).
            bool isFire = false;
            if (weapon != null && weapon.ActiveSpellEffects != null)
            {
                foreach (var eff in weapon.ActiveSpellEffects)
                {
                    if (eff != null && !string.IsNullOrEmpty(eff.BonusDamageType) &&
                        eff.BonusDamageType.IndexOf("fire", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isFire = true;
                        if (!string.IsNullOrEmpty(eff.BonusDamageDice))
                        {
                            string[] parts = eff.BonusDamageDice.Split('d');
                            if (parts.Length == 2 && int.TryParse(parts[0], out int bCount) && int.TryParse(parts[1], out int bDice))
                            {
                                for (int i = 0; i < bCount; i++)
                                    damage += UnityEngine.Random.Range(1, bDice + 1);
                            }
                        }
                        break;
                    }
                }
            }

            // Objects take half damage from ranged weapons, before hardness (PHB p.165), rounded down (PHB p.304).
            int dealt = isRanged ? damage / 2 : damage;

            sb.AppendLine("═══════════════════════════════════");
            sb.AppendLine($"🧊 {actor.Stats.CharacterName} attacks Wall of Ice at ({cell.x},{cell.y}) with {weaponName}{(isRanged ? " (ranged)" : "")}!");
            sb.AppendLine("  Auto-hit (stationary object)");
            sb.AppendLine($"  Damage: {damageRoll}{(isFire ? " (includes fire)" : "")}");
            if (isRanged)
                sb.AppendLine($"  Ranged weapon damage to an object is halved: {dealt}");

            wall.OnCellAttacked(actor, cell, dealt, isFire);

            if (wall.IsBreached(cell))
            {
                sb.AppendLine($"  💥 Wall cell ({cell.x},{cell.y}) breached!");
            }
            else
            {
                int maxHP = wall.CasterLevel * 3;
                sb.AppendLine($"  Cell HP: {wall.GetCellHP(cell)}/{maxHP}");
            }
            sb.Append("═══════════════════════════════════");
            o.CoreOutcome = wall.IsBreached(cell);

            // The shot spends its piece of ammunition and leaves a crossbow unloaded (PHB p.113, p.115), as every shot does.
            if (isRanged && weapon != null)
            {
                if (weapon.IsProjectileWeapon && _combatFlowService != null)
                    _combatFlowService.ConsumeAmmoForAttack(actor, weapon);
                if (weapon.RequiresReload)
                {
                    string reloadState = actor.OnWeaponFired(weapon);
                    if (!string.IsNullOrEmpty(reloadState))
                        Debug.Log($"[Reload] {actor.Stats.CharacterName}: {reloadState}");
                }
            }
        }

        CombatUI?.ShowCombatLog(sb.ToString());
        UpdateAllStatsUI();
        FinishWallAttackUi(source);
        return true;
    }

    /// <summary>For the player, closes the PC's attack flow after a wall attack: one standard action, no iterative attacks on the wall.</summary>
    private void FinishWallAttackUi(ActorSource source)
    {
        if (source != ActorSource.Ui)
            return;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingBreakWallTarget = false;
        EndAttackSequence();
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
}
