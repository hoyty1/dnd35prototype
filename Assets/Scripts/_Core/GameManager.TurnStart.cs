using System.Collections;
using UnityEngine;

/// <summary>
/// GameManager partial class: the start of a combatant's turn (batch 12 steps 5-6, docs/designs/pc_npc_parity_batch12.md;
/// docs/systems/PC_NPC_PARITY.md section 5).
///
/// Two routines hold the rules work of a turn start, the same for a PC and an NPC:
/// - <see cref="BeginCombatantTurn"/> runs once per turn from <c>OnTurnStarted</c>, before the turn routine's turn-skip
///   gate (<c>ShouldSkipTurnDueToHPState</c>), whether or not the creature can act: the ongoing effects, the cooldowns,
///   the per-turn resets and the turn-start combat-end check (decision D3).
/// - <see cref="BeginActingTurn"/> runs after the gate, only for a creature that acts: the perception check
///   (<see cref="PerceptionRules.ProcessTurnStart"/>, batch 12 step 8), the Turn Undead proximity check, the bearer's
///   aura (<see cref="ProcessTurnStartAura"/>, batch 13 step 19, D13) and the compulsion pass
///   (<see cref="TryRunCompulsionTurn"/>, batch 12 step 7), which stores the turn's compulsions on
///   <c>CurrentTurnStart</c> for both sides and starts a PC's forced turn.
///
/// The gate itself is <see cref="ConditionRules.DeniesAllActions(CharacterController)"/> (decision D4): only a creature
/// that can take no action at all loses its turn.
///
/// Barbarian rage passes at the barbarian's initiative count (<c>OnInitiativeCountReached</c>, <see cref="TickRageAtCount"/>),
/// like bardic music. <see cref="BeginCombatantTurn"/> never yields: the skip gate advances the turn synchronously
/// (CORE-012).
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The turn-start work of every creature, before the turn-skip gate (delegated RAW reading 2026-10-23, decision D3,
    /// docs/systems/RULES_COVERAGE.md 4.1: MM p.314, regeneration works every
    /// round; PHB p.138, durations run on the creature's own initiative count; PHB p.142, fighting defensively lasts until
    /// the creature's next turn). Returns true when the combat is over, in which case <c>OnTurnStarted</c> starts no turn
    /// routine. Concentration on a skipped turn keeps its own rule (<c>MaintainConcentrationOnSkippedTurn</c>).
    /// </summary>
    private bool BeginCombatantTurn(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return false;

        // Ongoing damage and healing at the start of the turn. A reaction to that damage (the bombardier beetle's acid
        // spray, D1) waits until the once-per-round flags are cleared, so it belongs to the new round.
        DeferAttackedReactions();
        try
        {
            HandleFlamingSphereTurnStart(actor);
            ApplyMelfsAcidArrowTurnStartDamage(actor);
            // Grease's repeated Reflex save for the creatures in the caster's areas, on the caster's turn (PHB p.237, D12).
            ApplyGreaseCasterTurnSaves(actor);
            actor.ApplyRegenerationAtTurnStart();

            // Breath weapon, ranged special and terrain manipulation cooldowns (AI-032), and the once-per-round flags
            // (the acid spray, MM p.285, D1; Cleave, PHB p.92, D7).
            actor.TickTurnCooldowns();
            actor.ClearOncePerRoundFlags();
        }
        finally
        {
            ResumeAttackedReactions();
        }

        // The aid bonuses this creature granted end unused at the start of its next turn (PHB p.154), also on a skipped turn.
        ExpireAidBonusesAtTurnStart(actor);

        // The per-turn resets: actions, attacks of opportunity, fighting defensively and total defense (both last until
        // the creature's next turn, PHB p.142; CMB-179, also on a skipped turn), a readied counterspell, movement.
        actor.StartNewTurn();
        // Last turn's compulsions are gone; an acting turn evaluates its own (TryRunCompulsionTurn).
        actor.CurrentTurnStart.Clear();

        // A bard's fascination whose bard is gone or dead ends before the skip gate, so the creature acts this turn, PC or
        // NPC (batch 12 step 7; before, only the AI branch released it, and the gate made that branch unreachable).
        ReleaseFascinationWithoutSource(actor);

        // Ongoing damage at the start of the turn can drop the last creature of a side: one check for both sides (CORE-003).
        return EvaluateCombatEnd("TurnStart");
    }

    /// <summary>
    /// The turn-start rules work that runs after the turn-skip gate, only for a creature that acts this turn, PC or NPC:
    /// the perception check (<see cref="PerceptionRules.ProcessTurnStart"/>: the squares of the enemies it sees, then one
    /// Listen check to pinpoint the unseen ones it tracks, DMG p.295; CMB-094, batch 12 step 8), the Turn Undead tracker
    /// prune and proximity check, the bearer's supernatural aura (<see cref="ProcessTurnStartAura"/>: once per round, for
    /// every bearer, AI-run, controllable or compelled; batch 13 step 19, delegated RAW reading 2026-10-29, decision D13;
    /// AI-053, CRE-039), then the compulsion pass (<see cref="TryRunCompulsionTurn"/>).
    /// Returns true when a compulsion took a PC's turn over as a forced turn, or when the aura ended the combat (the caller
    /// then starts nothing). The PC path calls it once the turn's phase and sub-phase are set; its UI resets (off-hand and
    /// dual-wield fields, the attack sequence, the logs) stay in <c>StartPCTurn</c>.
    /// </summary>
    private bool BeginActingTurn(CharacterController actor)
    {
        if (actor == null)
            return false;

        PerceptionRules.ProcessTurnStart(actor, GetAllCharacters(), msg => CombatUI?.ShowCombatLog(msg), this);
        PruneTurnUndeadTrackers();
        CheckTurnUndeadProximityBreakingForCleric(actor);

        // An aura that takes the last creature of a side out of the fight (a petrifying gaze) ends the combat here.
        if (actor.HasAuraAbility && ProcessTurnStartAura(actor) && EvaluateCombatEnd("TurnStart.Aura"))
            return true;

        return TryRunCompulsionTurn(actor);
    }

    /// <summary>
    /// The compulsion pass of an acting turn, the same for every actor (batch 12 step 7; delegated RAW reading 2026-10-23,
    /// decision D4, PHB p.159 turned, glossary p.310 frightened, p.311 panicked; owner decision 2026-10-23, Q1). It
    /// evaluates Confused (the d% roll, once), Suggested, Frightened or Panicked, and Turned from the shared controllers
    /// and stores the result in <c>actor.CurrentTurnStart.Compulsion</c>; the AI branches read it instead of rolling
    /// again. For a PC (the UI-controlled turn routine) a compulsion that takes the turn over starts a forced turn on the
    /// Confusion pattern: the sub-phase is Animating, the action buttons are hidden, the shared controller acts, then
    /// <c>EndCurrentTurn</c>. Returns true when it started one. An NPC's compulsions run in its AI turn, where the
    /// scenario hook still comes after the forced kinds. Charmed stays a target restriction (the PC menu half is batch 13).
    /// </summary>
    private bool TryRunCompulsionTurn(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return false;

        TurnCompulsion compulsion = EvaluateTurnCompulsion(actor);
        actor.CurrentTurnStart.Compulsion = compulsion;

        if (!IsPC(actor))
            return false;

        if (compulsion.Confused != null)
        {
            if (compulsion.ConfusionForcesTurn)
                return BeginForcedPCTurn(actor, ExecuteConfusedForcedPCTurn(actor, compulsion.Confused));
            CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actor.Stats.CharacterName} is confused but acts normally this turn."));
        }

        // A creature under a suggestion follows its course (the bard's Suggestion, PHB p.29; CHR-026).
        if (compulsion.Suggested != null)
            return BeginForcedPCTurn(actor, ExecuteSuggestedForcedPCTurn(actor, compulsion.Suggested));

        // A frightened or panicked PC flees through the shared flee controller, a turned one through the shared turned
        // flight (owner decision 2026-10-23, Q1: automatic flight until batch 13's restricted PC menu; CMB-100).
        if (compulsion.Fear != null || compulsion.Turned)
            return BeginForcedPCTurn(actor, ExecuteFleeingForcedPCTurn(actor, compulsion));

        return false;
    }

    /// <summary>The compulsions <paramref name="actor"/> is under now, from the shared controllers (rolls confusion once).</summary>
    private TurnCompulsion EvaluateTurnCompulsion(CharacterController actor)
    {
        var compulsion = new TurnCompulsion();

        if (TryGetConfusedTurnDecisionForAI(actor, out ConfusedBehaviorController.ConfusedTurnDecision confused))
            compulsion.Confused = confused;

        // A fascinated creature takes no actions at all, so its suggestion waits (the skip gate holds it anyway).
        if (!actor.HasCondition(CombatConditionType.Fascinated)
            && TryGetSuggestedTurnDecisionForAI(actor, out SuggestedBehaviorController.SuggestedTurnDecision suggested))
            compulsion.Suggested = suggested;

        if (TryGetFrightenedTurnDecisionForAI(actor, out FrightenedBehaviorController.FrightenedTurnDecision fear))
        {
            // A fear whose source is gone, dead or never resolved to a creature (a Mace of Terror's, CMB-178) ends here,
            // before the turn is decided, so the creature gets its normal turn instead of a flight that does nothing.
            CharacterController fearSource = fear.CasterSource;
            if (fearSource == null || fearSource.Stats == null || fearSource.Stats.IsDead)
                FrightenedBehaviorController.EndFearWithoutSource(this, actor);
            else
                compulsion.Fear = fear;
        }

        // Any turned creature flees (PHB p.159), whatever turned it; before batch 12 step 7 the AI branch also asked for
        // an undead, and the skip gate took every turned creature's turn.
        compulsion.Turned = actor.HasCondition(CombatConditionType.Turned);

        return compulsion;
    }

    /// <summary>Starts a PC's forced turn on the Confusion pattern: Animating, no action buttons, the controller acts.</summary>
    private bool BeginForcedPCTurn(CharacterController pc, IEnumerator forcedTurn)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.SetActionButtonsVisible(false);
        StartCoroutine(forcedTurn);
        return true;
    }

    /// <summary>
    /// A PC's forced flight (owner decision 2026-10-23, Q1): a prone PC stands up first, as the AI does before its own
    /// fear branch (PHB p.143, CMB-074); then the shared flee controller (Frightened, Panicked) or the shared turned
    /// flight (<see cref="AIService.ExecuteTurnedTurn"/>), and the turn ends. A frightened (not panicked) PC that could
    /// not flee gets its action menu back instead, fear penalties and fighting defensively in force: a cornered
    /// frightened creature may fight (PHB glossary p.310).
    /// </summary>
    private IEnumerator ExecuteFleeingForcedPCTurn(CharacterController pc, TurnCompulsion compulsion)
    {
        if (pc.HasCondition(CombatConditionType.Prone) && !pc.IsGrappling())
        {
            yield return StartCoroutine(TryStandUpFromProneForAI(pc));
            if (CurrentPhase == TurnPhase.CombatOver || ThreatSystem.IsMoverIncapacitated(pc))
            {
                if (CurrentPhase == TurnPhase.PCTurn && CurrentCharacter == pc)
                    EndCurrentTurn();
                yield break;
            }
        }

        if (compulsion.Fear != null)
        {
            bool panicked = pc.HasCondition(CombatConditionType.Panicked);
            var flight = new FrightenedBehaviorController.FlightResult();
            yield return StartCoroutine(ExecuteFrightenedTurnDecisionForAI(pc, compulsion.Fear, flight));
            if (!panicked && flight.Outcome == FrightenedBehaviorController.FlightOutcome.Cornered
                && CurrentPhase == TurnPhase.PCTurn && CurrentCharacter == pc)
            {
                CurrentSubPhase = PlayerSubPhase.ChoosingAction;
                CombatUI?.SetActionButtonsVisible(true);
                ShowActionChoices();
                yield break;
            }
        }
        else if (compulsion.Turned && _aiService != null)
            yield return StartCoroutine(_aiService.ExecuteTurnedTurn(pc));

        if (CurrentPhase == TurnPhase.PCTurn && CurrentCharacter == pc)
            EndCurrentTurn();
    }

    /// <summary>
    /// A creature fascinated by a bard's Fascinate whose bard is gone or dead is no longer fascinated: the performance
    /// ends with the bard (PHB p.29). Moved from the AI turn in batch 12 step 7 so that it applies to PCs and NPCs alike,
    /// before the skip gate. Only the bard's Fascinate: a spell's fascination (Hypnotism, Rainbow Pattern, Calm Animals,
    /// Calm Emotions) lasts its duration whether or not its caster resolves to a living creature (Rainbow Pattern records
    /// only the spell's name as its source).
    /// </summary>
    private void ReleaseFascinationWithoutSource(CharacterController actor)
    {
        if (!actor.HasCondition(CombatConditionType.Fascinated))
            return;
        if (!TryGetFascinatedTurnDecisionForAI(actor, out FascinatedBehaviorController.FascinatedTurnDecision decision))
            return;
        if (decision?.Condition == null || decision.Condition.SourceId != BardicMusicRules.FascinateSourceId)
            return;

        CharacterController source = decision != null ? decision.CasterSource : null;
        if (source != null && source.Stats != null && !source.Stats.IsDead)
            return;

        RemoveCondition(actor, CombatConditionType.Fascinated);
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("👁", $"{actor.Stats.CharacterName} is no longer fascinated."));
    }

    /// <summary>
    /// A round of a raging barbarian's rage passes at its initiative count, PC or NPC, whether or not it can act (PHB p.25,
    /// rage lasts a number of rounds; decision D3; CHR-069). Called from <c>OnInitiativeCountReached</c>.
    /// </summary>
    private void TickRageAtCount(CharacterController count)
    {
        if (count == null || count.Stats == null || !count.Stats.IsBarbarian || !count.Stats.IsRaging)
            return;

        count.Stats.TickRage();
        LogRageTick(count);
    }

    /// <summary>The combat log line of a rage tick: the rounds left, or the end of the rage and the fatigue.</summary>
    private void LogRageTick(CharacterController barbarian)
    {
        CharacterStats s = barbarian.Stats;
        if (!s.IsRaging)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"😫 {s.CharacterName}'s rage has ended! Now fatigued."));
            UpdateAllStatsUI();
        }
        else
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡", $"{s.CharacterName}: Rage - {s.RageRoundsRemaining} rounds remaining"));
        }
    }
}
