using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One charge, PC or NPC (batch 12 step 21, docs/designs/pc_npc_parity_batch12.md; CMB-018, CMB-108, CMB-013; decision
/// D2). Built by <c>GameManager.ChargeRequestForUi</c> (the Charge button and the Bull Rush (Charge) menu button) or
/// <c>GameManager.ChargeRequestForAI</c> (the NPC turn path), checked by <c>GameManager.CanCharge</c> and run by
/// <c>GameManager.ExecuteCharge(ChargeRequest, ChargeOutcome)</c>.
/// </summary>
public sealed class ChargeRequest
{
    public CharacterController Charger;
    public CharacterController Target;

    /// <summary>The charge path (null: the shortest legal one, GameManager.GetChargePath).</summary>
    public List<Vector2Int> Path;

    /// <summary>
    /// A partial charge (PHB p.154, D2): the charger is limited to one standard or move action this turn (disabled,
    /// staggered, slowed, a zombie's single actions), so the charge is a standard action and moves up to its speed, not
    /// double. Set by the builders from <see cref="ActionEconomy.SingleActionOnly"/>.
    /// </summary>
    public bool Partial;

    /// <summary>The charge ends in a bull rush instead of an attack (PHB p.154-155).</summary>
    public bool BullRush;

    /// <summary>An overrun as part of a charge (PHB p.157). Not built yet: <c>CanCharge</c> refuses it (CMB-197).</summary>
    public bool Overrun;

    /// <summary>The source of the charger's choices (null: the charger's own, AttackDeciders.For; the NPC turn path: AI).</summary>
    public ActorSource? DeciderSource;

    /// <summary>The request comes from the PC UI (its intent is read from the UI, AttackIntent.ForUi), else from the AI.</summary>
    public bool FromUi;

    // Presentation of the two flows (the only differences between the builders).
    public float SecondsPerStep = 0.06f;
    public float AoODelay = 0.5f;
    public string AoOLabel = "AoO during charge";
    public string CombatEndContext = "ExecuteCharge.Final";
}

/// <summary>What <c>GameManager.ExecuteCharge(ChargeRequest, ChargeOutcome)</c> did, for the thin PC and NPC wrappers.</summary>
public sealed class ChargeOutcome
{
    /// <summary>The charge was refused before anything was paid (<see cref="RefusedReason"/>).</summary>
    public bool Refused;
    public string RefusedReason;

    /// <summary>The charge's action was paid and the charger set off.</summary>
    public bool Started;

    /// <summary>The charge was a partial charge (a standard action, PHB p.154, D2).</summary>
    public bool Partial;

    /// <summary>An AoO on the way stopped the charger (dropped, tripped, grabbed); the charge ends there.</summary>
    public bool InterruptedByAoO;
    public string InterruptReason;

    /// <summary>
    /// An AoO dropped the charger at the start or during the push of a bull rush charge (ResolveChargeBullRush): the charge
    /// ends there. The PC wrapper ends the PC's turn; nothing else is done for it (formerly EndChargeBullRushForDroppedCharger).
    /// </summary>
    public bool ChargerDropped;

    /// <summary>The charger failed its save against the target's Sanctuary or Hide from Undead: the attack is lost (D6).</summary>
    public bool LostToWard;

    public bool Flanking;
    public bool Pounce;
    public bool SmiteDeclared;

    /// <summary>Spirited Charge's weapon damage multiplier on the charge attack (PHB p.100), 1 for none.</summary>
    public int DamageMultiplier = 1;

    /// <summary>The attack or pounce (null for a bull rush, a lost attack or an interrupted charge).</summary>
    public AttackSequenceOutcome Attacks;

    public bool CombatEnded;
}

/// <summary>The charge's action rule (PHB p.154; D2), pure: no dice, no log. PCs and NPCs alike.</summary>
public static class ChargeRules
{
    /// <summary>
    /// The action <paramref name="charger"/> can charge with this turn: a full-round action, or, for a creature limited to
    /// one standard or move action (<see cref="ActionEconomy.SingleActionOnly"/>: disabled, staggered, slowed, single
    /// actions only), a partial charge as a standard action (<paramref name="partial"/> true; PHB p.154, p.280; D2). A
    /// creature limited to a move action (nauseated) cannot charge: it cannot attack (D2, DMG p.301).
    /// </summary>
    public static bool HasChargeAction(CharacterController charger, out bool partial, out string reason)
    {
        partial = false;
        reason = string.Empty;
        ActionEconomy actions = charger != null ? charger.Actions : null;
        if (actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (actions.MoveActionOnly)
        {
            reason = "A creature limited to a move action (nauseated) cannot charge: it cannot attack.";
            return false;
        }

        if (actions.SingleActionOnly)
        {
            partial = true;
            if (!actions.HasStandardAction)
            {
                reason = "Need the turn's single action for a partial charge.";
                return false;
            }
            return true;
        }

        if (!actions.HasFullRoundAction)
        {
            reason = "Need full-round action to charge.";
            return false;
        }

        return true;
    }
}

public partial class GameManager
{
    /// <summary>A charge request from the PC UI (the Charge button, or the menu's Bull Rush (Charge)): the player's choices and timing.</summary>
    internal ChargeRequest ChargeRequestForUi(CharacterController charger, CharacterController target, bool bullRush)
    {
        return new ChargeRequest
        {
            Charger = charger,
            Target = target,
            BullRush = bullRush,
            Partial = charger != null && charger.Actions != null && charger.Actions.SingleActionOnly,
            DeciderSource = null,
            FromUi = true,
            SecondsPerStep = PlayerMoveSecondsPerStep,
            AoODelay = 0.8f,
            AoOLabel = "AoO during charge",
            CombatEndContext = "ExecuteCharge.Final",
        };
    }

    /// <summary>A charge request from the NPC turn path (AI-run for every creature on it): the AI's choices and timing.</summary>
    internal ChargeRequest ChargeRequestForAI(CharacterController charger, CharacterController target, bool bullRush)
    {
        return new ChargeRequest
        {
            Charger = charger,
            Target = target,
            BullRush = bullRush,
            Partial = charger != null && charger.Actions != null && charger.Actions.SingleActionOnly,
            DeciderSource = ActorSource.AI,
            FromUi = false,
            SecondsPerStep = NpcChargeMoveSecondsPerStep,
            AoODelay = 0.5f,
            AoOLabel = charger != null && charger.Stats != null ? $"AoO vs {charger.Stats.CharacterName}" : "AoO during charge",
            CombatEndContext = "NPCExecuteCharge.Final",
        };
    }

    /// <summary>
    /// May this charge be made now (PHB p.154-155)? Pure: no dice, no log; the PC button, the PC flow and the AI read it.
    /// An enemy target, more than one square away; the charge's action (a full-round action, or a partial charge as a
    /// standard action for a creature limited to one action, D2; <see cref="ChargeRules.HasChargeAction"/>); no 5-foot step
    /// this turn; standing (CMB-074) and not grappling (PHB p.156); a melee attack, or the bull rush's own rules; the hold
    /// bar (CMB-163); the sphere and a known failed ward (AttackLegality, D6); not fatigued, exhausted or entangled; and a
    /// clear path of at least 10 ft and at most double the speed (the speed for a partial charge).
    /// </summary>
    internal Legality CanCharge(ChargeRequest req)
    {
        CharacterController charger = req != null ? req.Charger : null;
        CharacterController target = req != null ? req.Target : null;
        if (charger == null || target == null || charger == target)
            return Legality.Refused("no charger or target");
        if (charger.Stats == null || target.Stats == null || target.Stats.IsDead)
            return Legality.Refused("the target is gone");

        if (req.Overrun)
            return Legality.Refused("an overrun as part of a charge is not built yet (CMB-197)");

        if (!TeamUtility.IsEnemy(charger, target))
            return Legality.Refused("You can only charge an enemy target.");

        int startingDistanceSquares = GetChargeStartingDistanceSquares(charger, target);
        if (startingDistanceSquares <= ChargeBlockedDistanceSquares)
            return Legality.Refused($"Cannot charge {target.Stats.CharacterName}: target is within {ChargeBlockedDistanceSquares} squares ({startingDistanceSquares} squares away) from your starting position.");

        if (!ChargeRules.HasChargeAction(charger, out bool partial, out string actionReason))
            return Legality.Refused(actionReason);
        if (partial != req.Partial)
            return Legality.Refused(partial
                ? "Only a partial charge is possible: the charger is limited to one action this turn (PHB p.154)."
                : "A partial charge is only for a creature limited to one action (PHB p.154).");

        if (charger.HasTakenFiveFootStep)
            return Legality.Refused("Cannot charge after taking a 5-foot step.");

        // A prone creature cannot charge: it must stand up first, which spends the move action a charge needs (PHB p.143,
        // CMB-074). Shared by the PC button and the AI.
        if (charger.HasCondition(CombatConditionType.Prone))
            return Legality.Refused("Prone creatures cannot charge; stand up first.");

        // No normal movement while grappling (PHB p.156); the charge budget below would otherwise still allow the 2-square
        // minimum. Shared by the PC button and the AI.
        if (charger.IsGrappling())
            return Legality.Refused("Grappling creatures cannot charge.");

        // A bull rush needs no weapon (PHB p.154); it needs the shared size, swarm, incorporeal and grappling rules instead.
        // Adjacency is the charge path's job (endpoints touch the target).
        if (req.BullRush)
        {
            if (!charger.CanBullRush(target, true, out string bullRushReason))
                return Legality.Refused($"Cannot bull rush {target.Stats.CharacterName}: {bullRushReason}.");
        }
        else if (!charger.HasMeleeWeaponEquipped())
        {
            return Legality.Refused("Need a melee weapon (or natural/unarmed attack) to charge.");
        }
        // A creature holding the target with one body part may not attack it, so it cannot charge it (MM p.310; delegated
        // RAW reading 2026-10-14, CMB-163).
        else if (charger.IsBarredFromOrdinaryAttackOn(target, out string chargeHoldReason))
        {
            return Legality.Refused(chargeHoldReason);
        }

        // The direct-attack gate, read only (AttackLegality, batch 12 step 12), for the attack or the bull rush at the end of
        // the charge: nothing passes a Resilient Sphere (PHB p.258, CMB-092), and a target whose ward the charger failed to
        // overcome cannot be attacked (D6). The path test below keeps the charge's own reach. An unrolled ward is legal; the
        // attack after the move rolls its save.
        Legality chargeLegality = AttackLegality.Check(charger, target, DirectAttackKind.Charge,
            req.BullRush ? AttackIntent.ForManeuver(SpecialAttackType.BullRushCharge) : null,
            AttackLegalityChecks.Sphere | AttackLegalityChecks.Ward);
        if (!chargeLegality.Ok)
            return Legality.Refused($"Cannot charge {target.Stats.CharacterName}: {chargeLegality.Reason}.");

        if (charger.Stats.IsFatiguedOrExhausted)
            return Legality.Refused($"{(charger.Stats.IsExhaustedState ? "Exhausted" : "Fatigued")} creatures cannot charge.");

        if (charger.HasCondition(CombatConditionType.Entangled))
            return Legality.Refused("Entangled creatures cannot charge.");

        // The charge's movement through the shared movement legality (MovementService.CanStartMovement, batch 12 step 22):
        // the checks above keep their own wording; this one adds what only it decides (a condition that prevents movement,
        // no speed this turn, a disabled creature's spent turn) so the charge and every other movement read one rule.
        Legality chargeMovement = MovementService.CanStartMovement(charger, MoveKind.Charge);
        if (!chargeMovement.Ok)
            return Legality.Refused($"Cannot charge: {chargeMovement.Reason}.");

        // A charge moves at least 10 ft and at most its budget (PHB p.154): a charger whose budget is shorter (a slowed
        // 15-ft creature's partial charge of 5 ft) cannot charge at all.
        int chargeBudget = GetChargeMaxDistanceSquares(charger);
        if (chargeBudget < MinChargeMovementSquares)
            return Legality.Refused($"Cannot move the {MinChargeMovementSquares * 5} ft a charge needs (at most {chargeBudget * 5} ft{(partial ? " on a partial charge" : string.Empty)}).");

        if (!TryBuildChargePath(charger, target, out _, out _, out ChargePathFailureReason failureReason, req.BullRush))
        {
            switch (failureReason)
            {
                case ChargePathFailureReason.TooClose:
                    return Legality.Refused($"Cannot charge targets within {ChargeBlockedDistanceSquares} squares ({ChargeBlockedDistanceSquares * 5} ft). Move at least {MinChargeMovementSquares} squares ({MinChargeMovementSquares * 5} ft).");
                case ChargePathFailureReason.TooFar:
                    return Legality.Refused($"Target is too far to charge (max {GetChargeMaxDistanceSquares(charger) * 5} ft{(partial ? ", a partial charge moves up to the speed" : string.Empty)}).");
                default:
                    return Legality.Refused("No valid charge path to target.");
            }
        }

        return Legality.Allowed(chargeLegality.Risk);
    }

    /// <summary>
    /// The longest charge in squares: double the speed, or the speed for a partial charge (PHB p.154, D2; a slowed
    /// creature's speed is already halved). No floor: a charger whose budget is under the 10-ft minimum cannot charge at
    /// all (CanCharge refuses it), rather than moving beyond its speed.
    /// </summary>
    private int GetChargeMaxDistanceSquares(CharacterController charger)
    {
        int speed = Mathf.Max(0, GetCurrentMoveRangeSquares(charger));
        bool partial = charger != null && charger.Actions != null && charger.Actions.SingleActionOnly;
        return partial ? speed : speed * 2;
    }

    /// <summary>
    /// The one charge executor for PCs and NPCs (batch 12 step 21; CMB-018, CMB-108; PHB p.154-155). It checks the request
    /// (<see cref="CanCharge"/>), pays the charge's action (the full-round action, or the standard action of a partial
    /// charge, D2: CharacterController.CommitStandardAction, so a disabled charger's act is strenuous, PHB p.145), moves one
    /// square at a time with each provoked AoO resolved before its step (PHB p.137; ResolveMovementAoOsBeforeStep), then
    /// resolves the end of the charge (<see cref="ResolveChargeEnd"/>), applies the charge's -2 AC until the charger's next
    /// turn and checks the end of combat. It does no UI continuation: the thin wrappers (the PC's ExecuteCharge and the
    /// NPC's NPCExecuteCharge) do.
    /// </summary>
    internal IEnumerator ExecuteCharge(ChargeRequest req, ChargeOutcome o)
    {
        if (o == null)
            o = new ChargeOutcome();
        Legality legal = CanCharge(req);
        if (!legal.Ok)
        {
            o.Refused = true;
            o.RefusedReason = legal.Reason;
            yield break;
        }

        CharacterController charger = req.Charger;
        CharacterController target = req.Target;
        List<Vector2Int> path = req.Path != null && req.Path.Count > 0 ? req.Path : GetChargePath(charger, target, req.BullRush);
        if (path == null || path.Count == 0)
        {
            o.Refused = true;
            o.RefusedReason = "Charge aborted: invalid path.";
            yield break;
        }

        // Pay: a partial charge is a standard action (PHB p.154, D2), any other charge the full-round action.
        if (req.Partial)
        {
            if (!charger.CommitStandardAction())
            {
                o.Refused = true;
                o.RefusedReason = $"{charger.Stats.CharacterName} cannot charge: no standard action remaining.";
                yield break;
            }
        }
        else
        {
            charger.Actions.UseFullRoundAction();
        }
        o.Started = true;
        o.Partial = req.Partial;

        string charges = req.Partial ? "makes a partial charge" : "charges";
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", req.BullRush
            ? $"🏇 {charger.Stats.CharacterName} {charges} and attempts a bull rush on {target.Stats.CharacterName}!"
            : $"🏇 {charger.Stats.CharacterName} {charges}{(req.Partial ? " at" : string.Empty)} {target.Stats.CharacterName}!"));

        // Resolve AoOs before each step (shared helper, PHB p.137) so the charge stops in place if an AoO drops, trips or
        // otherwise stops the charger.
        Vector2Int chargeStart = charger.GridPosition;
        var provokedAoOs = CheckForAoO(charger, path);
        bool interruptedByAoO = false;
        var aooOutcome = new MovementAoOStepOutcome();
        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            // A step the charger cannot take (a barrier, a hold, Grease's failed Balance check) provokes nothing: it never
            // leaves its square (PHB p.137, p.237). The charge stops there.
            if (MovementService.IsStepRefused(charger, charger.GridPosition, path[pathIndex], MoveKind.Charge))
            {
                interruptedByAoO = true;
                aooOutcome.StopMovement = true;
                aooOutcome.Reason = "stopped on the way";
                break;
            }

            yield return StartCoroutine(ResolveMovementAoOsBeforeStep(charger, provokedAoOs, pathIndex, req.AoOLabel, req.AoODelay, aooOutcome));
            if (aooOutcome.StopMovement)
            {
                interruptedByAoO = true;
                break;
            }

            var stepPath = new List<Vector2Int> { path[pathIndex] };
            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(charger, stepPath, req.SecondsPerStep, markAsMoved: false, kind: MoveKind.Charge));
            else
                yield return StartCoroutine(charger.MoveAlongPath(stepPath, req.SecondsPerStep, markAsMoved: false, kind: MoveKind.Charge));

            // Refused or ended on the way (a barrier, a movement step rule such as Grease's Balance check, an area's damage):
            // the charge stops there rather than jump to the next square (batch 12 step 23).
            if (charger.GridPosition != path[pathIndex])
            {
                interruptedByAoO = true;
                aooOutcome.StopMovement = true;
                aooOutcome.Reason = ThreatSystem.IsMoverIncapacitated(charger) ? "incapacitated" : "stopped on the way";
                break;
            }
        }

        charger.HasMovedThisTurn = true;

        CheckTurnUndeadProximityBreakingForMover(charger);
        PruneTurnUndeadTrackers();

        if (interruptedByAoO)
        {
            o.InterruptedByAoO = true;
            o.InterruptReason = aooOutcome.Reason;
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔", $"{charger.Stats.CharacterName}'s charge is interrupted ({aooOutcome.Reason})."));
            UpdateAllStatsUI();
            o.CombatEnded = CurrentPhase == TurnPhase.CombatOver;
            yield break;
        }

        InvalidatePreviewThreats();

        IEnumerator end = ResolveChargeEnd(req, o, chargeStart, path);
        while (end.MoveNext())
            yield return end.Current;

        if (o.ChargerDropped)
        {
            o.CombatEnded = CurrentPhase == TurnPhase.CombatOver;
            yield break;
        }
        if (CurrentPhase == TurnPhase.CombatOver)
        {
            o.CombatEnded = true;
            yield break;
        }

        // D&D 3.5e: the charge's AC penalty lasts until the start of this charger's next turn (PHB p.154).
        ApplyChargePenaltyUntilStartOfNextTurn(charger);
        CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{charger.Stats.CharacterName} is charging: -2 AC until next turn."));
        UpdateAllStatsUI();

        Debug.Log($"[Charge] Charge ending | attacker={charger.Stats.CharacterName} | target={target?.Stats?.CharacterName ?? "<null>"} | partial={req.Partial} | targetDead={(target != null && target.Stats != null && target.Stats.IsDead)} | phase={CurrentPhase}");

        // Victory or defeat for either side when the charge (or an AoO on the way) dropped the last creature of a side (CORE-011).
        CheckCombatVictory(req.CombatEndContext, target);
        o.CombatEnded = CurrentPhase == TurnPhase.CombatOver;
    }

    /// <summary>
    /// The end of a charge, PC and NPC alike, from the square where it ended: flanking (PHB p.153, CombatUtils.IsAttackerFlanking,
    /// per step in the attack sequence); the charger's one save against a ward (D6), whose failure loses the attack or bull
    /// rush; the smite (the AI's declaration here, TryNPCDeclareSmite; the PC's with its Smite button before the charge;
    /// both set CharacterStats.DeclaredSmite, which the attack reads; SmiteRules, CHR-020); Spirited Charge's multiplier for
    /// a mounted charger (PHB p.100); then the bull rush (ResolveChargeBullRush, CMB-102) or the attack sequence
    /// (AttackSequenceRequest.ForCharge): one charge attack, or a pounce (MM p.313) whose natural attacks and rakes are its steps.
    /// </summary>
    private IEnumerator ResolveChargeEnd(ChargeRequest req, ChargeOutcome o, Vector2Int chargeStart, List<Vector2Int> path)
    {
        CharacterController charger = req.Charger;
        CharacterController target = req.Target;

        o.Flanking = CombatUtils.IsAttackerFlanking(charger, target, GetAllCharacters(), out _);

        // Sanctuary and Hide from Undead (PHB p.274, p.241; D6): the attack (or bull rush) after the move is the attempt, and
        // rolls the charger's one save; a failure loses it (the charge's action is spent).
        o.LostToWard = AttackLegality.CommitWard(charger, target, DirectAttackKind.Charge,
            req.BullRush ? AttackIntent.ForManeuver(SpecialAttackType.BullRushCharge) : null) == WardVerdict.Failed;
        if (o.LostToWard)
            yield break; // logged by the commit

        if (req.BullRush)
        {
            var bullRushOutcome = new ChargeBullRushOutcome();
            yield return StartCoroutine(ResolveChargeBullRush(charger, target,
                SquareGridUtils.CalculatePathCost(chargeStart, path), CountDiagonalSteps(chargeStart, path), bullRushOutcome));
            o.ChargerDropped = bullRushOutcome.AttackerIncapacitated;
            yield break;
        }

        // The AI may smite with the charge's attack (or a pounce's first attack), as a PC may declare its smite before
        // charging (SmiteRules, CHR-020). A bull rush on a charge makes no attack, so it declares nothing.
        if (!req.FromUi)
            TryNPCDeclareSmite(charger, target);
        o.SmiteDeclared = charger.Stats.DeclaredSmite != SmiteSource.None;

        // A pounce rides on the charge: MM p.313 lets the creature follow any charge with a full attack, and a partial
        // charge is a charge (PHB p.154), so a pouncer limited to one action (slowed, disabled, staggered) still pounces after its
        // partial charge; the single-action limit bars taking the full-attack action, not the charge's own rider
        // (delegated RAW reading 2026-10-29, RULES_COVERAGE 4.1 D2 row).
        o.Pounce = charger.Stats.HasPounce && charger.Stats.HasNaturalAttacks && charger.GetEquippedMainWeapon() == null;
        IAttackDecider decider = AttackDeciders.For(charger, req.DeciderSource);
        AttackIntent intent = req.FromUi ? AttackIntent.ForUi(this, charger) : AttackIntent.ForAI(this, charger, target);
        int hasteIndex = -1;
        if (o.Pounce)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Summon("🐅", $"{charger.Stats.CharacterName} uses Pounce and unleashes a full natural attack at the end of the charge!"));
            // A pounce is a full attack, so Haste adds one natural attack, the charger's pick (PHB p.239; owner decision
            // 2026-10-07, CMB-106; CMB-124). Cancel uses the default. The chooser's labels add the charge's +2 (PHB p.154).
            if (charger.CanUseHasteExtraNaturalAttack())
            {
                Coroutine hasteChoice = decider.ChooseHasteNaturalAttack(this, charger, target, null, 2, choice => hasteIndex = choice);
                if (hasteChoice != null)
                    yield return hasteChoice;
            }
        }
        else
        {
            o.DamageMultiplier = MountedCombatSystem.GetChargeDamageMultiplier(charger, charger.GetEquippedMainWeapon());
        }

        string flankText = o.Flanking ? " + Flanking" : string.Empty;
        bool foe = !req.FromUi;
        System.Action<AttackSequenceStep> report = step =>
        {
            if (CombatUI == null || step == null || step.Result == null)
                return;
            string line = o.Pounce
                ? $"Charge Pounce (+2{flankText}): {step.Result.GetAttackBreakdown(step.Label)}"
                : $"Charge Attack (+2{flankText}): {step.Result.GetDetailedSummary()}";
            CombatUI.ShowCombatLog(foe ? CombatLogHelper.Death("☠", line) : CombatLogHelper.Damage("⚡", line));
        };

        AttackSequenceRequest attack = AttackSequenceRequest.ForCharge(charger, target, intent, decider, o.Pounce, hasteIndex, o.DamageMultiplier, report);
        var attackOutcome = new AttackSequenceOutcome();
        o.Attacks = attackOutcome;
        IEnumerator run = RunAttackSequence(attack, attackOutcome);
        while (run.MoveNext())
            yield return run.Current;

        if (!attackOutcome.CombatEnded && charger.Stats != null)
        {
            if (o.Pounce && attackOutcome.StartedGrapple)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"{charger.Stats.CharacterName} is grappling: the pounce's remaining attacks are not made as ordinary attacks (PHB p.156)."));
            else if (attackOutcome.AttacksMade == 0)
                // The one charge attack (or every pounce step) was stopped before it was rolled (stage-3 legality, reach):
                // say so, so the charge never ends silently.
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", string.IsNullOrEmpty(attackOutcome.StopReason)
                    ? $"{charger.Stats.CharacterName}'s charge ends without an attack."
                    : $"{charger.Stats.CharacterName}'s charge ends without an attack: {attackOutcome.StopReason}"));
            else if (attackOutcome.StoppedByLegality && !string.IsNullOrEmpty(attackOutcome.StopReason))
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", attackOutcome.StopReason));
        }
    }

    /// <summary>
    /// One step of a charge's attack sequence (<see cref="RunAttackSequence"/>, AttackSequenceKind.Charge): a pounce's
    /// natural attack <paramref name="stepIndex"/> (CharacterController.FullAttack one attack at a time, as the Full Attack
    /// button steps) with the charge's +2 as a bonus entry for that step (PHB p.154), recorded on the result so a Cleave
    /// after it repeats it (PHB p.92, D7); or the one charge attack (CharacterController.ResolveChargeAttack, with Spirited
    /// Charge's multiplier). Null when nothing was resolved (no natural attack left).
    /// </summary>
    private CombatResult ResolveChargeStep(CharacterController charger, CharacterController target, AttackSequenceRequest req,
        int stepIndex, bool isFlanking, int flankBonus, string partnerName, out string label)
    {
        label = null;
        if (!req.ChargePounce)
        {
            label = "Charge attack";
            return charger.ResolveChargeAttack(target, isFlanking, flankBonus, partnerName, req.ChargeDamageMultiplier);
        }

        FullAttackResult stepResult;
        charger.Stats.Bonuses.Set(CharacterStats.ChargeBonusSource, BonusTarget.AttackRoll, BonusType.Untyped, ChargeAttackBonus, "Charge"); // PHB p.154
        try
        {
            stepResult = charger.FullAttack(target, isFlanking, flankBonus, partnerName, null,
                startAttackIndex: stepIndex, maxAttacks: 1, hasteNaturalAttackIndex: req.ChargeHasteNaturalAttackIndex);
        }
        finally
        {
            charger.Stats.Bonuses.Set(CharacterStats.ChargeBonusSource, BonusTarget.AttackRoll, BonusType.Untyped, 0);
        }

        if (stepResult == null || stepResult.Attacks == null || stepResult.Attacks.Count == 0)
            return null;

        label = stepResult.AttackLabels != null && stepResult.AttackLabels.Count > 0 ? stepResult.AttackLabels[0] : $"Attack {stepIndex + 1}";
        CombatResult attack = stepResult.Attacks[0];
        if (attack != null && attack.SituationalAttackBonus == 0)
        {
            attack.SituationalAttackBonus = ChargeAttackBonus;
            attack.SituationalAttackLabel = "Charge";
        }
        return attack;
    }

    /// <summary>
    /// A pounce's rakes after its natural attacks (MM p.313: no grapple needed; delegated RAW reading 2026-10-14, CMB-144),
    /// with the charge's +2, then each rake hit's Concentration check and melee reactions. None once the charger is out of
    /// the fight or the target dead, and none on a target the charger holds with one body part (it may not attack it, MM
    /// p.310, CMB-163).
    /// </summary>
    private void ResolvePounceRakes(CharacterController charger, CharacterController target, AttackSequenceOutcome o)
    {
        if (charger == null || charger.Stats == null || !charger.Stats.HasRake || CombatEndRules.IsOutOfFight(charger)
            || target == null || target.Stats == null || target.Stats.IsDead)
            return;
        if (charger.IsBarredFromOrdinaryAttackOn(target, out _))
            return;

        bool isFlanking = CombatUtils.IsAttackerFlanking(charger, target, GetAllCharacters(), out CharacterController partner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = partner != null && partner.Stats != null ? partner.Stats.CharacterName : null;

        FullAttackResult rakeResult;
        charger.Stats.Bonuses.Set(CharacterStats.ChargeBonusSource, BonusTarget.AttackRoll, BonusType.Untyped, ChargeAttackBonus, "Charge");
        try
        {
            rakeResult = charger.PerformRakeAttacks(target, isFlanking, flankBonus, partnerName);
        }
        finally
        {
            charger.Stats.Bonuses.Set(CharacterStats.ChargeBonusSource, BonusTarget.AttackRoll, BonusType.Untyped, 0);
        }
        ResolvePounceRakeAftermath(charger, target, rakeResult);
        if (rakeResult != null && rakeResult.Attacks != null)
        {
            for (int i = 0; i < rakeResult.Attacks.Count; i++)
            {
                o.Aggregate.Attacks.Add(rakeResult.Attacks[i]);
                o.Aggregate.AttackLabels.Add(rakeResult.AttackLabels != null && i < rakeResult.AttackLabels.Count ? rakeResult.AttackLabels[i] : "Rake");
                o.AttacksMade++;
            }
        }
    }
}
