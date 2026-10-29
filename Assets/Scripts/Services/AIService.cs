using System;
using System.Collections;
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Profiles;
using DND35.Magic;
using UnityEngine;
using DND35e.Identifiers;
using Random = UnityEngine.Random;

/// <summary>
/// Centralized NPC AI orchestration and tactical decision-making.
/// GameManager delegates hostile turn decisions and target/movement evaluation to this service.
/// </summary>
public class AIService : MonoBehaviour
{
    public enum AIDifficultyLevel
    {
        Easy,
        Normal,
        Hard
    }

    public enum AIActionType
    {
        Wait,
        Move,
        Attack,
        SpecialManeuver,
        Charge,
        Retreat
    }

    private GameManager _gameManager;

    [SerializeField] private AIDifficultyLevel _difficulty = AIDifficultyLevel.Normal;

    /// <summary>
    /// The creatures that cast and held a touch charge this turn to move and touch (a HoldCharge candidate, batch 11 step
    /// 27), with the foe they meant to touch: the turn's end moves them into reach and delivers it.
    /// </summary>
    private readonly Dictionary<CharacterController, CharacterController> _heldChargePlans = new Dictionary<CharacterController, CharacterController>();

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void Cleanup()
    {
        _gameManager = null;
    }

    /// <summary>
    /// True once victory or defeat has been declared (GameManager.EvaluateCombatEnd, CORE-011). A turn in progress
    /// stops at its next step: the AI neither moves nor acts after the combat is over.
    /// </summary>
    private bool CombatEnded()
        => _gameManager == null || _gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver;

    /// <summary>
    /// The context of a save against a creature's aura (ExecuteAura): a fear aura is a fear effect for the bonuses that
    /// apply against fear (SaveRules, CHR-018); a charm or compulsion is an enchantment (the elf's +2, Still Mind) and a
    /// charm gets Inspire Courage's bonus; a sonic aura (a harpy's song, a gibbering mouther's gibbering) can be met with
    /// a bard's countersong (PHB p.29; BardicMusicRules.ApplyCountersong, CHR-026).
    /// </summary>
    public static SaveContext AuraSaveContext(AuraAbilityDefinition aura, CombatConditionType conditionType)
    {
        if (aura == null)
            return SaveContext.None;
        return new SaveContext(enchantment: aura.IsEnchantment || aura.IsCharm, fear: conditionType == CombatConditionType.Frightened,
            label: aura.Name, charm: aura.IsCharm, sonicOrLanguageDependent: aura.IsSonic);
    }

    /// <summary>
    /// Whether <see cref="ExecuteNPCTurn"/> runs a non-summoned creature with this behaviour and profile through the
    /// ranged-kiter routine: with a profile, a healer (its default route; it melees only when
    /// <see cref="HealerAIProfile.DetermineCombatMode"/> picks melee for a physical attack, a stat comparison made at
    /// run time), not a dragon (its own routine), not DefensiveMelee, otherwise RangedKiter or a Ranged
    /// <see cref="AIProfile.CombatStyle"/>; without a profile, RangedKiter only (<c>NPCAIBehavior.Ranged</c> is not
    /// routed, AI-003). The spawn uses the same test to decide which weapon a creature starts holding
    /// (<see cref="Inventory.EquipStartingLoadout"/>, ITM-004).
    /// </summary>
    internal static bool RoutesToRangedTurn(NPCAIBehavior behavior, AIProfile profile)
    {
        if (profile == null)
            return behavior == NPCAIBehavior.RangedKiter;
        if (profile is HealerAIProfile)
            return true;
        if (profile is DragonAIProfile)
            return false;
        if (behavior == NPCAIBehavior.DefensiveMelee)
            return false;
        return behavior == NPCAIBehavior.RangedKiter || profile.CombatStyle == CombatStyle.Ranged;
    }

    /// <summary>
    /// The NPC's turn (<see cref="ExecuteNPCTurnBody"/>), then, when the AI cast and held a touch charge this turn to
    /// reach a foe out of reach (a HoldCharge candidate, batch 11 step 27, SPL-115), the move into reach and the touch
    /// (<see cref="TryDeliverHeldChargeTurn"/>; PHB p.141: in the casting round the caster may move between casting and
    /// touching).
    /// </summary>
    public IEnumerator ExecuteNPCTurn(CharacterController npc, NPCAIBehavior behavior)
    {
        if (npc != null)
        {
            _heldChargePlans.Remove(npc);
        }

        yield return _gameManager != null ? _gameManager.StartCoroutine(ExecuteNPCTurnBody(npc, behavior)) : null;

        if (npc != null && _heldChargePlans.TryGetValue(npc, out CharacterController plannedTarget))
        {
            _heldChargePlans.Remove(npc);
            if (!CombatEnded() && npc.Stats != null && npc.CanTakeTurnActions())
                yield return _gameManager.StartCoroutine(TryDeliverHeldChargeTurn(npc, plannedTarget));
        }
    }

    private IEnumerator ExecuteNPCTurnBody(CharacterController npc, NPCAIBehavior behavior)
    {
        if (_gameManager == null || npc == null || npc.Stats == null || CombatEnded())
            yield break;

        // The turn-start effects, resets and perception already ran (GameManager.OnTurnStarted, SingleNPCTurnFromInitiative).

        bool isSummon = _gameManager.IsSummonedCreature(npc);
        string turnColor = isSummon ? "#66E8FF" : "#FF6666";
        string turnIcon = isSummon ? "✶" : "💀";

        _gameManager.CombatUI?.SetTurnIndicator($"{_gameManager.GetSummonDisplayName(npc)}'s turn...");
        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Summon("", $"<color={turnColor}>{turnIcon} {_gameManager.GetSummonDisplayName(npc)}'s turn begins</color>"));
        yield return new WaitForSeconds(0.6f);
        if (CombatEnded())
            yield break;

        // The turn's compulsions were evaluated once at its start, for every actor (GameManager.TryRunCompulsionTurn, batch 12
        // step 7, decision D4): the branches below read them instead of rolling or building them again.
        TurnCompulsion compulsion = npc.CurrentTurnStart.Compulsion ?? new TurnCompulsion();

        ConfusedBehaviorController.ConfusedTurnDecision confusedDecision = compulsion.Confused;
        if (confusedDecision != null)
        {
            if (confusedDecision.Mode != ConfusedBehaviorController.ConfusedTurnMode.ActNormally)
            {
                yield return _gameManager.StartCoroutine(_gameManager.ExecuteConfusedTurnDecisionForAI(npc, confusedDecision));
                yield break;
            }

            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{npc.Stats.CharacterName} is confused but acts normally."));
        }

        // Charm only restricts targets: a suggestion, fear or turning takes the turn over first, as on a PC's turn
        // (GameManager.TryRunCompulsionTurn; one precedence for both sides, batch 12 step 7).
        bool compelledBeyondCharm = compulsion.Suggested != null || compulsion.Fear != null || compulsion.Turned;
        if (!compelledBeyondCharm && _gameManager.TryGetCharmedTurnDecisionForAI(npc, out CharmedBehaviorController.CharmedTurnDecision charmedDecision))
        {
            // A charmed creature still acts on its own, so it stands up first like any other
            // prone NPC (CMB-074); a fascinated one takes no actions.
            if (npc.HasCondition(CombatConditionType.Prone) && !npc.IsGrappling() && !npc.HasCondition(CombatConditionType.Fascinated))
            {
                yield return _gameManager.StartCoroutine(_gameManager.TryStandUpFromProneForAI(npc));
                if (CombatEnded() || ThreatSystem.IsMoverIncapacitated(npc))
                    yield break;
            }

            yield return _gameManager.StartCoroutine(_gameManager.ExecuteCharmedTurnDecisionForAI(npc, charmedDecision));
            yield break;
        }

        // A fascinated creature takes no actions: the turn-skip gate skips it (decision D4), and a fascination whose source
        // is gone ends at its turn start for both sides (GameManager.BeginCombatantTurn), so this is only a safety net.
        if (_gameManager.TryGetFascinatedTurnDecisionForAI(npc, out FascinatedBehaviorController.FascinatedTurnDecision fascinatedDecision))
        {
            CharacterController source = fascinatedDecision != null ? fascinatedDecision.CasterSource : null;
            string sourceName = source != null && source.Stats != null ? source.Stats.CharacterName : "nothing";
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("👁", $"{npc.Stats.CharacterName} stares blankly at {sourceName} and takes no actions."));
            yield return new WaitForSeconds(0.25f);
            yield break;
        }

        // A creature under a suggestion follows its course instead of acting (the bard's Suggestion, PHB p.29 and p.285;
        // the same behaviour as a suggested PC's forced turn, CHR-026).
        if (compulsion.Suggested != null)
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteSuggestedTurnDecisionForAI(npc, compulsion.Suggested));
            yield break;
        }

        // ── Scenario harness scripted turn (test instrumentation; null in normal play) ──
        // Runs after the turn-start rules and the HP, confused, charmed, fascinated and suggested gates. A
        // script replaces everything below: the automatic stand-up, the frightened, panicked and turned
        // flight, the Animate Rope escape, the no-target search, the grapple turn, free auras and spittle
        // and the Resilient Sphere restriction. The harness returns null for Frightened, Panicked or Turned
        // actors, so the forced kinds stay with the AI, and for any actor it wants the AI to run
        // (ScenarioHooks.ScriptedTurn).
        IEnumerator scripted = ScenarioHooks.ScriptedTurn?.Invoke(npc);
        if (scripted != null)
        {
            yield return _gameManager.StartCoroutine(scripted);
            yield break;
        }

        // ── Stand up from prone (move action, provokes; PHB p.143, CMB-074) ──
        // A prone creature cannot take ordinary movement, so it stands first through the same
        // resolution as the PC Stand Up button. Grappling creatures stay down (grapple turn below).
        if (npc.HasCondition(CombatConditionType.Prone) && !npc.IsGrappling())
        {
            yield return _gameManager.StartCoroutine(_gameManager.TryStandUpFromProneForAI(npc));
            if (CombatEnded())
                yield break;
            if (ThreatSystem.IsMoverIncapacitated(npc))
            {
                Debug.Log($"[AI] {npc.Stats.CharacterName} was dropped by an AoO while standing up — turn ended");
                yield break;
            }
        }

        // A frightened or panicked creature flees from the fear's source (PHB glossary p.310, p.311); the same controller
        // runs a PC's forced flight (GameManager.TryRunCompulsionTurn, owner decision 2026-10-23, Q1).
        if (compulsion.Fear != null)
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteFrightenedTurnDecisionForAI(npc, compulsion.Fear));
            yield break;
        }

        // A turned creature flees from the turner, or cowers when it cannot (PHB p.159; decision D4). Before the target
        // search, so a turned creature that sees no foe still flees instead of searching.
        if (compulsion.Turned)
        {
            yield return _gameManager.StartCoroutine(ExecuteTurnedTurn(npc));
            yield break;
        }

        // An Improved Grab hold with one body part (MM p.310; CMB-148, CMB-149): the AI keeps holding while another foe is
        // within reach to fight; otherwise it lets go (a free action on its turn, delegated RAW reading 2026-10-14, CMB-163)
        // and acts normally. When to let go is an AI design item (AI-064).
        if (npc.TryGetImprovedGrabHold(out CharacterController heldFoe, out _)
            && !AIProfile.HasOtherEnemyInReach(npc, heldFoe, _gameManager.GetAllCharactersForAI()))
        {
            string released = npc.ReleaseImprovedGrabHold("no other foe within reach");
            if (!string.IsNullOrEmpty(released))
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", released));
        }

        // Still holding: the hold's Damage check (at -20) in place of its first attack, when it keeps even odds
        // (MM p.310; the same shared path as the PC's Grapple: Damage button, CMB-163).
        if (npc.IsHoldingWithImprovedGrab())
        {
            yield return _gameManager.StartCoroutine(_gameManager.AI_ImprovedGrabHoldDamageCheck(npc));
            if (CombatEnded())
                yield break;
        }

        if (_gameManager.TryExecuteAnimateRopeEscapeForNpc(npc))
        {
            yield return new WaitForSeconds(0.35f);
            if (CombatEnded())
                yield break;
        }

        CharacterController targetPC = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (targetPC == null)
        {
            yield return _gameManager.StartCoroutine(ExecuteSearchTurnWhenNoTargets(npc));
            if (CombatEnded())
                yield break;
            targetPC = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
            if (targetPC == null)
            {
                string npcName = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{npcName} cannot find a target and keeps searching the battlefield."));
                yield break;
            }

            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} spots {targetPC.Stats.CharacterName} after searching."));
        }

        if (npc.IsGrappling())
        {
            // A grappling caster may keep its spell up (ConcentrationRules; delegated RAW reading 2026-10-15, PHB p.156,
            // p.176): the same choice as below, against a grapple attack as the other standard action.
            if (TryMaintainConcentrationTurn(npc, targetPC, grappling: true))
            {
                yield return new WaitForSeconds(0.5f);
                yield break;
            }
            // A grappler may cast a spell the grapple allows (PHB p.156; CMB-101): the cast candidates of the ordinary turn,
            // each through the shared cast legality (CheckLegalityForAI meets GrappleSpellcastRefusal), in place of its grapple
            // actions.
            if (TryGrappledSpellcastForAI(npc, targetPC))
            {
                yield return new WaitForSeconds(0.5f);
                yield break;
            }
            yield return _gameManager.StartCoroutine(RunGrappleTurnForAI(npc));
            if (CombatEnded() || !CanContinueTurnAfterGrappleEnded(npc)) yield break; // free pin release (CMB-089)
        }

        // ── Supernatural Aura (free action): Gibbering, Frightful Presence, etc. ──
        // Per D&D 3.5e, supernatural auras activate automatically each round.
        // Process before other actions since it's a free action.
        if (npc.HasAuraAbility)
        {
            ProcessAuraAbility(npc);
            if (_gameManager.EvaluateCombatEnd("AI.Aura"))
                yield break;
        }

        // ── Free-Action Ranged Special Attack (Spittle, etc.) ──
        // Per MM p.126, Gibbering Mouther's Spittle is a free action each round.
        // Fire at start of turn (after Gibbering aura) if any enemy is within range.
        // This does NOT consume a standard or move action — the creature still acts normally.
        if (npc.HasRangedSpecialAttack)
        {
            RangedSpecialAttackDefinition freeAttack = npc.GetRangedSpecialAttackDefinition();
            if (freeAttack != null && freeAttack.IsFreeAction && npc.IsRangedSpecialAttackReady)
            {
                CharacterController spittleTarget = FindClosestEnemyInRange(npc, freeAttack.RangeFeet);
                if (spittleTarget != null)
                {
                    Debug.Log($"[AI] {npc.Stats.CharacterName} fires free-action {freeAttack.Name} at {spittleTarget.Stats.CharacterName}");
                    TryExecuteRangedSpecialAttack(npc, spittleTarget);
                    if (_gameManager.EvaluateCombatEnd("AI.FreeRangedSpecial"))
                        yield break;
                    // No yield break — creature continues its turn with standard/move actions
                }
            }
        }

        // ── Resilient Sphere: NPC inside sphere can move within it but attacks/spells
        // cannot pass the boundary (PHB p.263). The sphere is now a stationary area effect.
        if (ResilientSphereAreaEffect.IsCharacterInAnySphere(npc))
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("🔮", $"{npc.Stats.CharacterName} is enclosed in a Resilient Sphere — attacks and spells cannot pass through the boundary!"));
            // NPC skips offensive actions but could still move within sphere
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        AIProfile profile = GetProfile(npc);
        if (profile is SwarmAI swarmProfile)
        {
            yield return _gameManager.StartCoroutine(ExecuteSwarmTurn(npc, swarmProfile, profile is IndiscriminateSwarmAI));
            yield break;
        }

        // Lay on Hands (Su, PHB p.44): healing a hurt creature within touch, or destroying an undead foe the pool can
        // finish, with the standard action, through the rules the PC button calls (LayOnHandsRules, CHR-020). The AI
        // ends its turn after it (a simplification: it does not move afterwards).
        if (!isSummon && _gameManager.TryNPCLayOnHandsTurnForAI(npc))
        {
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        if (isSummon)
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteSummonedCreatureTurnForAI(npc));
            yield break;
        }

        // A touch charge held from an earlier turn (PHB p.141: the caster may keep making touch attacks round after round;
        // batch 11 step 27, SPL-115): move into reach if needed and touch the best target, through the delivery the PC's
        // Discharge button runs. A missed touch keeps the charge. The delivery costs what the PC's does (batch 12 step 18,
        // SPL-156): an attack (the standard action) for a foe, a standard action for a friend; the turn goes on with what is left.
        if (npc.Spellcasting != null && npc.Spellcasting.HasHeldTouchCharge && !npc.IsGrappling())
        {
            yield return _gameManager.StartCoroutine(TryDeliverHeldChargeTurn(npc, targetPC));
            if (CombatEnded() || ThreatSystem.IsMoverIncapacitated(npc))
                yield break;
        }

        // Bardic music (PHB p.29-30): a song, its concentration or a suggestion with the standard action, through the rules
        // the PC Bardic Music button calls (BardicMusicRules, CHR-026), when AIProfile.ChooseBardicMusic picks one; the AI
        // ends its turn after it (a simplification: it does not move afterwards). Stopping a song costs no action, and the
        // turn goes on (to cast while Inspire Courage lingers).
        if (_gameManager.TryNPCBardicMusicTurnForAI(npc))
        {
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        // A maintained spell (PHB p.176): keeping it going is this turn's standard action, through the rule the PC's
        // Maintain button calls (ConcentrationRules, decision D2, SPL-153), when AIProfile.ShouldMaintainConcentration
        // finds it worth more than the other standard action; the AI ends its turn after it (a simplification, as for
        // bardic music: it does not move). Otherwise the turn goes on, and a cast or the end of the turn ends the spell.
        if (TryMaintainConcentrationTurn(npc, targetPC))
        {
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        // Wild Shape (Su, PHB p.37): the druid takes an animal form with its standard action, through the rules the PC
        // button calls (WildShapeRules, CHR-020), when AIProfile.ChooseWildShapeForm picks one; then it closes in with its
        // move action. In animal form it fights in melee with the form's natural attacks, whatever its usual shell (it
        // cannot cast without Natural Spell, and its gear is melded).
        if (_gameManager.TryNPCWildShapeTurnForAI(npc))
        {
            yield return _gameManager.StartCoroutine(MoveTowardTargetAfterStandardAction(npc));
            yield break;
        }
        if (WildShapeRules.IsWildShaped(npc.Stats))
        {
            yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
            yield break;
        }

        if (profile != null)
        {
            if (profile is HealerAIProfile healerProfile)
            {
                List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();
                bool hasCastableSpells = HasCastablePreparedSpells(npc);
                HealerActionType actionType = healerProfile.DetermineActionPriority(npc, allCombatants, hasCastableSpells);

                Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} action priority: {actionType}");

                if (actionType == HealerActionType.PhysicalAttack)
                {
                    CombatStyle physicalStyle = healerProfile.DetermineCombatMode(npc);
                    if (physicalStyle == CombatStyle.Ranged)
                    {
                        yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                    }
                    else if (behavior == NPCAIBehavior.DefensiveMelee)
                    {
                        yield return _gameManager.StartCoroutine(ExecuteDefensiveMeleeTurn(npc, targetPC));
                    }
                    else
                    {
                        yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                    }

                    yield break;
                }

                if (actionType == HealerActionType.CriticalHealing || actionType == HealerActionType.Healing)
                {
                    CharacterController healTarget = healerProfile.GetPriorityHealTarget(npc, allCombatants);
                    if (healTarget != null && healTarget.Stats != null)
                    {
                        Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} targeting {healTarget.Stats.CharacterName} for healing.");
                        // T1.1 FIX: Actually attempt to cast heal spell on the ally target
                        bool healCasted = TryExecuteSpellcastAction(npc, healTarget);
                        if (healCasted)
                        {
                            Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} successfully healed {healTarget.Stats.CharacterName}.");
                            yield return new WaitForSeconds(0.5f);
                            yield break;
                        }
                        Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} failed to heal — falling back to ranged kiter.");
                    }
                }

                if (actionType == HealerActionType.Buffing)
                {
                    // T1.1 FIX: Actually attempt to cast buff spell (ally targeting now works)
                    bool buffCasted = TryExecuteSpellcastAction(npc, null);
                    if (buffCasted)
                    {
                        Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} successfully cast buff spell.");
                        yield return new WaitForSeconds(0.5f);
                        yield break;
                    }
                }

                // Spell execution handled by ranged kiter turn (includes offensive spells)
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                yield break;
            }

            // ── Dragon / breath-weapon tactical AI ──
            if (profile is DragonAIProfile dragonProfile)
            {
                yield return _gameManager.StartCoroutine(ExecuteDragonTurn(npc, targetPC, dragonProfile));
                yield break;
            }

            // Profile drives targeting/maneuvers, while NPCAIBehavior still selects tactical shell.
            if (behavior == NPCAIBehavior.DefensiveMelee)
            {
                yield return _gameManager.StartCoroutine(ExecuteDefensiveMeleeTurn(npc, targetPC));
            }
            else if (RoutesToRangedTurn(behavior, profile))
            {
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
            }
            else
            {
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
            }

            yield break;
        }

        switch (behavior)
        {
            case NPCAIBehavior.AggressiveMelee:
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                break;
            case NPCAIBehavior.RangedKiter:
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                break;
            case NPCAIBehavior.DefensiveMelee:
                yield return _gameManager.StartCoroutine(ExecuteDefensiveMeleeTurn(npc, targetPC));
                break;
            default:
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                break;
        }
    }

    private IEnumerator ExecuteSearchTurnWhenNoTargets(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || npc.Actions == null)
            yield break;

        string npcName = npc.Stats.CharacterName;
        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npcName} has no valid targets and starts searching."));

        if (!npc.Actions.HasMoveAction || npc.Stats.MovementBlockedByCondition)
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{npcName} cannot move to search this turn."));
            yield break;
        }

        Vector2Int searchDestination;
        if (!TryFindSearchDestination(npc, out searchDestination) || searchDestination == npc.GridPosition)
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npcName} scans the area but finds no better position."));
            yield break;
        }

        Debug.Log($"[AI][Search] {npcName} moving from {npc.GridPosition} to {searchDestination}");

        var searchMove = new MoveAttempt();
        yield return _gameManager.StartCoroutine(
            _gameManager.MoveCharacterAlongComputedPathForAI(npc, searchDestination, _gameManager.GetPlayerMoveSecondsPerStepForAI(), searchMove));
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
            yield break; // dropped by an AoO or area damage while moving (CMB-073)
        if (searchMove.Refused)
            yield break; // refused by the shared movement legality (logged): nothing spent

        if (npc.Actions.HasMoveAction)
            npc.Actions.UseMoveAction();

        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npcName} moves to search for enemies."));
        yield return new WaitForSeconds(0.35f);
    }

    private bool TryFindSearchDestination(CharacterController npc, out Vector2Int destination)
    {
        destination = npc != null ? npc.GridPosition : Vector2Int.zero;

        if (npc == null || npc.Stats == null || _gameManager == null || _gameManager.Grid == null)
            return false;

        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        AIProfile profile = GetProfile(npc);
        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();

        SquareCell bestTrackedCell = null;
        int bestTrackedDistance = int.MaxValue;

        if (tracker != null && allCombatants != null)
        {
            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController candidate = allCombatants[i];
                if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                    continue;
                if (!TeamUtility.IsEnemy(npc, candidate))
                    continue;
                if (!tracker.HasLastKnownPosition(candidate))
                    continue;

                Vector2Int? knownPosition = tracker.GetLastKnownPosition(candidate);
                if (!knownPosition.HasValue)
                    continue;

                SquareCell candidateCell = EvaluateMovementOptions(npc, knownPosition.Value, retreat: false, candidate, profile);
                if (candidateCell == null || candidateCell.Coords == npc.GridPosition)
                    continue;

                int distToKnown = SquareGridUtils.GetDistance(candidateCell.Coords, knownPosition.Value);
                if (distToKnown < bestTrackedDistance)
                {
                    bestTrackedDistance = distToKnown;
                    bestTrackedCell = candidateCell;
                }
            }
        }

        if (bestTrackedCell != null)
        {
            destination = bestTrackedCell.Coords;
            return true;
        }

        Vector2Int mapCenter = new Vector2Int(_gameManager.Grid.Width / 2, _gameManager.Grid.Height / 2);
        int npcMoveRange = _gameManager.GetCurrentMoveRangeSquares(npc);
        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(npc.GridPosition, npcMoveRange);

        SquareCell bestExplorationCell = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null || cell.Coords == npc.GridPosition)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, npc.GetVisualSquaresOccupied(), npc))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(npc, cell.Coords, avoidThreats: false, maxRangeOverride: npcMoveRange);
            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            int distToCenter = SquareGridUtils.GetDistance(cell.Coords, mapCenter);
            float score = -distToCenter + UnityEngine.Random.Range(0f, 4f);

            if (score > bestScore)
            {
                bestScore = score;
                bestExplorationCell = cell;
            }
        }

        if (bestExplorationCell == null)
            return false;

        destination = bestExplorationCell.Coords;
        return true;
    }

    /// <summary>
    /// The turned creature's turn, PC or NPC (PHB p.159): it flees from the turner by the best and fastest means, through
    /// the one flight search (<see cref="FrightenedBehaviorController.Flee"/>: a grapple escape first, then withdraw, run
    /// or a move), and when it cannot flee it cowers (the Cowering condition until its next turn, decision D4; the
    /// glossary condition, Dex denial included, is the delegated reading recorded in RULES_COVERAGE 4.1). The AI turn
    /// calls it for an NPC, <c>GameManager.TryRunCompulsionTurn</c> for a PC's forced turn (owner decision 2026-10-23, Q1).
    /// </summary>
    public IEnumerator ExecuteTurnedTurn(CharacterController npc)
    {
        if (npc == null || npc.Stats == null)
            yield break;

        CharacterController source = _gameManager.GetTurnUndeadTurnerForAI(npc);

        if (source == null)
        {
            List<StatusEffect> activeConditions = npc.GetActiveConditions();
            for (int i = 0; i < activeConditions.Count; i++)
            {
                StatusEffect condition = activeConditions[i];
                if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Turned)
                    continue;

                if (string.IsNullOrWhiteSpace(condition.SourceName))
                    break;

                List<CharacterController> all = _gameManager.GetAllCharactersForAI();
                for (int c = 0; c < all.Count; c++)
                {
                    CharacterController candidate = all[c];
                    if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                        continue;

                    if (string.Equals(candidate.Stats.CharacterName, condition.SourceName, StringComparison.Ordinal))
                    {
                        source = candidate;
                        _gameManager.RegisterTurnUndeadTrackerForAI(npc, source);
                        break;
                    }
                }
                break;
            }
        }

        if (source == null)
            source = _gameManager.GetClosestAliveEnemyToForAI(npc);

        var flight = new FrightenedBehaviorController.FlightResult();
        if (source != null)
            yield return _gameManager.StartCoroutine(FrightenedBehaviorController.Flee(_gameManager, npc, source, " (turned)", flight));
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (flight.Outcome == FrightenedBehaviorController.FlightOutcome.Fled)
            yield break;

        // Cornered: it cowers (PHB p.159; the glossary's cowering, p.307: no actions, -2 AC, no Dex bonus) until its next
        // turn, when the compulsion pass looks again.
        _gameManager.ApplyCondition(npc, CombatConditionType.Cowering, 1, source, sourceNameOverride: "Turned (cornered)", sourceCategory: "Turned");
        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{npc.Stats.CharacterName} is turned and cannot flee: it cowers, unable to act."));
        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>
    /// After a standard action that is not an attack (a wild shape), the move action: toward the best target when it
    /// is out of reach, by the same movement choice as the aggressive melee turn. Nothing else this turn.
    /// </summary>
    private IEnumerator MoveTowardTargetAfterStandardAction(CharacterController npc)
    {
        if (CombatEnded() || npc == null || npc.Stats == null || !npc.CanTakeTurnActions() || npc.Actions == null || !npc.Actions.HasMoveAction)
            yield break;
        if (npc.Stats.MovementBlockedByCondition)
            yield break;

        CharacterController target = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (target == null || target.Stats == null || npc.IsTargetInCurrentWeaponRange(target))
            yield break;

        SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, GetProfile(npc));
        if (bestCell == null)
            yield break;

        var advanceMove = new MoveAttempt();
        yield return _gameManager.StartCoroutine(
            _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), advanceMove));
        if (CombatEnded() || !npc.CanTakeTurnActions() || advanceMove.Refused)
            yield break; // a refused move (logged) spends nothing
        npc.Actions.UseMoveAction();
        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!"));
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator ExecuteAggressiveMeleeTurn(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        // Death/disable check: NPC may have been killed by damage before this method runs
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} cannot act (HP={npc.Stats.CurrentHP}) at start of aggressive melee turn — turn ended");
            yield break;
        }

        AIProfile profile = GetProfile(npc);

        // ── Debug: log special ability status ──
        if (npc.HasRangedSpecialAttack || npc.HasAuraAbility || npc.HasEngulf || npc.HasTerrainManipulation)
        {
            Debug.Log($"[AI][SpecialAbilities] {npc.Stats.CharacterName}: " +
                $"RangedSpecial={npc.HasRangedSpecialAttack} (ready={npc.IsRangedSpecialAttackReady}), " +
                $"Aura={npc.HasAuraAbility}, Engulf={npc.HasEngulf}, Terrain={npc.HasTerrainManipulation}");
        }

        // ── Special ability priorities before movement ──
        // Standard-action ranged special attacks (NOT free-action ones like Spittle).
        // Free-action attacks are already fired at turn start in ExecuteNPCTurn.
        if (npc.HasRangedSpecialAttack)
        {
            RangedSpecialAttackDefinition preMovAttack = npc.GetRangedSpecialAttackDefinition();
            if (preMovAttack != null && !preMovAttack.IsFreeAction)
            {
                int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
                int rangeSquares = preMovAttack.RangeFeet / 5;
                
                if (distance <= rangeSquares)
                {
                    Debug.Log($"[AI] {npc.Stats.CharacterName} attempting ranged special attack (distance {distance} squares, range {rangeSquares} squares)");
                    if (TryExecuteRangedSpecialAttack(npc, target))
                    {
                        _gameManager.EvaluateCombatEnd("AI.RangedSpecial");
                        yield return new WaitForSeconds(0.8f);
                        yield break;
                    }
                }
            }
        }

        // An overrun is a move through the target's space (PHB p.157; batch 12 step 20, D11), planned before the move and
        // made through the one overrun the PC uses; the attack follows when the standard action is left (an avoided overrun
        // costs none). An AI stopgap (AIProfile.ShouldOverrun) until the batch 13 catalog.
        bool overran = false;
        if (profile != null && profile.ShouldOverrun(npc, target)
            && _gameManager.TryPlanOverrunPathForAI(npc, target, out List<Vector2Int> overrunPath))
        {
            var overrun = new OverrunMoveOutcome();
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteOverrunForAI(npc, overrunPath, overrun));
            if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
            if (!npc.CanTakeTurnActions() || !npc.Actions.HasStandardAction)
                yield break;
            overran = overrun.Started;
        }

        AIActionType action = overran ? AIActionType.Attack : SelectBestAction(npc, target, preferAggression: true);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target, ShouldNPCChargeBullRush(npc, target)));
            yield break;
        }

        // Moving needs the move action (AI-034): after a search move the NPC does not move again.
        bool triedApproach = false;
        if (!npc.IsTargetInCurrentWeaponRange(target) && npc.Actions.HasMoveAction)
        {
            triedApproach = true;
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                var advanceMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), advanceMove));

                // ── Death/disable check after movement ──
                // Creature may have been killed by area damage (Wall of Fire, etc.) during movement.
                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dropped during movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!advanceMove.Refused) // a refused move (logged) spends nothing; the routine goes on
                {
                    npc.Actions.UseMoveAction();

                    // Activate terrain manipulation after moving closer
                    if (npc.HasTerrainManipulation)
                        ActivateTerrainManipulation(npc);

                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!"));
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} cannot act before attack phase (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        // The attack target is chosen among the targets the attack guard allows (batch 13 step 5, D1): another foe in reach
        // when the one it closed on is out of reach; without one, the routine's other options, then the profile's (C1).
        List<CharacterController> attackPool = _gameManager.GetAllCharactersForAI();
        CharacterController attackTarget = SelectGuardPassingTarget(npc, attackPool, allowThrow: false);
        target = attackTarget ?? SelectBestTarget(npc, attackPool);
        if (target == null)
            yield break;

        if (attackTarget != null && !target.Stats.IsDead)
        {
            // ── Try Engulf attack if adjacent and available ──
            if (npc.HasEngulf && SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition) == 1)
            {
                Debug.Log($"[AI] {npc.Stats.CharacterName} attempting engulf");
                if (TryExecuteEngulf(npc, target))
                {
                    yield return new WaitForSeconds(0.8f);
                    yield break;
                }
            }

            // Fall back to normal melee attacks
            // Each melee attack step may be replaced by a maneuver (CMB-102).
            yield return _gameManager.StartCoroutine(PerformMeleeAttackActionWithManeuvers(npc, target, profile));
        }
        else
        {
            // ── Post-movement ranged special attack ──
            // If we moved but still can't reach melee range, try standard-action ranged special attack.
            // Free-action attacks (Spittle) were already fired at turn start — skip them here.
            if (npc.HasRangedSpecialAttack && npc.IsRangedSpecialAttackReady)
            {
                RangedSpecialAttackDefinition postMovAttack = npc.GetRangedSpecialAttackDefinition();
                if (postMovAttack != null && !postMovAttack.IsFreeAction)
                {
                    int postMoveDistance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
                    int rangeSquares = postMovAttack.RangeFeet / 5;
                    if (postMoveDistance <= rangeSquares)
                    {
                        Debug.Log($"[AI] {npc.Stats.CharacterName} attempting post-movement ranged special attack (distance {postMoveDistance} squares, range {rangeSquares} squares)");
                        if (TryExecuteRangedSpecialAttack(npc, target))
                        {
                            _gameManager.EvaluateCombatEnd("AI.PostMoveRangedSpecial");
                            yield return new WaitForSeconds(0.8f);
                            yield break;
                        }
                    }
                }
            }

            // ── AI: Wall of Ice interaction ──
            // If the NPC cannot reach the target but is adjacent to a Wall of Ice,
            // try to attack or break through the wall to clear the path.
            yield return _gameManager.StartCoroutine(TryAIWallInteraction(npc, target));

            // A routine that did not try to close in (it thought the target in reach, or had no move): the profile's
            // alternatives (C1).
            if (!triedApproach && !CombatEnded() && npc.CanTakeTurnActions())
                yield return _gameManager.StartCoroutine(RunOutOfReachAlternatives(npc, target, CombatStyle.Melee, allowThrow: false));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Dragon Tactical Turn
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes a full dragon tactical turn following the priority:
    /// 1. Cast buff spells (if not in melee)
    /// 2. Cast attack spells (if not in melee)
    /// 3. Avoid AoOs
    /// 4. Prioritize healers/mages (unless closer target)
    /// 5. Use breath weapon when available to hit most targets
    /// 6. Full melee attack as last resort
    /// </summary>
    /// <summary>
    /// A spell the dragon's cast-first step may choose (AI-037): never a 0-level spell, and with
    /// <paramref name="buffsOnly"/> only a self or ally buff (an Illusion such as Mirror Image counts when it targets
    /// the caster or an ally).
    /// </summary>
    internal static bool IsDragonCastFirstCandidate(SpellData spell, bool buffsOnly)
    {
        if (spell == null || spell.SpellLevel < 1)
            return false;
        if (!buffsOnly)
            return true;
        if (spell.EffectType == SpellEffectType.Buff)
            return true;
        return spell.EffectType == SpellEffectType.Illusion && AISpellcastingStrategist.IsAllyTargetedSpell(spell);
    }

    /// <summary>
    /// The AI's choice to keep its concentration spell going this turn (AIProfile.ShouldMaintainConcentration), and the
    /// shared executor when it does (GameManager.TryMaintainConcentrationForAI). True when the standard action was spent.
    /// </summary>
    /// <param name="grappling">The caster is grappling: its other standard action is a grapple attack, not a spell or a
    /// weapon attack (the grapple turn's own choices, RunGrappleTurnForAI).</param>
    private bool TryMaintainConcentrationTurn(CharacterController npc, CharacterController target, bool grappling = false)
    {
        if (npc == null || _gameManager == null || !ConcentrationRules.CanMaintain(npc, out _))
            return false;
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        int otherSpellLevel = -1;
        bool canAttackNow = true;
        if (!grappling)
        {
            SpellData otherSpell = SelectSpell(npc, target);
            otherSpellLevel = otherSpell != null ? otherSpell.SpellLevel : -1;
            canAttackNow = false;
            for (int i = 0; i < all.Count && !canAttackNow; i++)
            {
                CharacterController foe = all[i];
                canAttackNow = foe != null && foe != npc && foe.Stats != null && !foe.IsDead && _gameManager.IsEnemyTeamForAI(npc, foe)
                    && AiAttackGuard.Passes(npc, foe, allowThrow: false); // the attack guard the attack branches ask (D1): reach, range, ammunition
            }
        }
        bool holdsSummon = _gameManager.HasConcentrationSummonForAI(npc);
        AIProfile profile = GetProfile(npc);
        bool maintain = profile != null
            ? profile.ShouldMaintainConcentration(npc, all, otherSpellLevel, canAttackNow, holdsSummon)
            : AIProfile.DefaultShouldMaintainConcentration(npc, all, otherSpellLevel, canAttackNow, holdsSummon);
        Debug.Log($"[AI][Concentration] {npc.Stats.CharacterName} on {npc.Concentration.ConcentrationSpellName}: otherSpellLevel={otherSpellLevel} attackNow={canAttackNow} summon={holdsSummon} grappling={grappling} -> {(maintain ? "maintain" : "let it lapse")}");
        return maintain && _gameManager.TryMaintainConcentrationForAI(npc);
    }

    private bool HasAdjacentEnemy(CharacterController npc, List<CharacterController> allCombatants)
    {
        if (npc == null || allCombatants == null)
            return false;
        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController c = allCombatants[i];
            if (c == null || c == npc || c.Stats == null || c.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, c))
                continue;
            if (SquareGridUtils.GetDistance(npc.GridPosition, c.GridPosition) <= 1)
                return true;
        }
        return false;
    }

    private IEnumerator ExecuteDragonTurn(CharacterController npc, CharacterController target, DragonAIProfile dragonProfile)
    {
        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI][Dragon] {npc.Stats.CharacterName} cannot act (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();

        // Reset breath weapon decision state
        dragonProfile.WantsToUseBreathWeapon = false;
        dragonProfile.BreathWeaponAimTarget = null;

        // ── Priority 1 & 2: Cast buff/attack spells if not in melee ──
        // AI-037 (partial, 2026-10-14): since SPL-123 the NPC cast executor accepts a dragon's known spells, so this
        // step used to win every turn with a cantrip in place of the breath or a full attack (0-level spells were free
        // until SPL-138; they now spend the dragon's 0-level spells per day, but a CL 1 dragon still has 5 of them, and
        // no expected-value comparison exists yet). Only a spell of 1st level or higher with a positive score is considered; while the breath is
        // ready or an enemy is adjacent (a full attack is on), only a buff not yet active (SelectSpell skips active ones).
        if (!dragonProfile.IsTooCloseForCasting(npc, allCombatants, _gameManager))
        {
            bool buffsOnly = (npc.HasBreathWeapon && npc.IsBreathWeaponReady) || HasAdjacentEnemy(npc, allCombatants);
            Func<SpellData, bool> dragonCastFilter = s => IsDragonCastFirstCandidate(s, buffsOnly);
            if (npc.Stats.IsSpellcaster && TryExecuteSpellcastAction(npc, target, dragonCastFilter, 0f))
            {
                Debug.Log($"[AI][Dragon] {npc.Stats.CharacterName} cast a spell (priority 1/2).");
                yield return new WaitForSeconds(0.8f);
                yield break;
            }
        }

        // ── Priority 5: Tactical Breath Weapon Positioning ──
        // When breath weapon is available, evaluate ALL reachable positions to find
        // the optimal spot that maximizes enemy hits. Move there, then fire.
        if (npc.HasBreathWeapon && npc.IsBreathWeaponReady)
        {
            Debug.Log($"🐉 [AI][Dragon] {npc.Stats.CharacterName} — breath weapon READY, initiating tactical positioning...");

            var bestBreathPos = dragonProfile.FindBestBreathPosition(npc, allCombatants, _gameManager);

            if (bestBreathPos.HasValue)
            {
                var bp = bestBreathPos.Value;

                // ── Step 1: Move to optimal position if needed ──
                if (bp.RequiresMove && npc.Actions.HasMoveAction)
                {
                    Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName} moving from {npc.GridPosition} → {bp.Position} " +
                              $"for breath weapon ({bp.EnemyHits} targets)");

                    var breathMove = new MoveAttempt();
                    yield return _gameManager.StartCoroutine(
                        _gameManager.MoveCharacterAlongComputedPathForAI(npc, bp.Position, _gameManager.GetPlayerMoveSecondsPerStepForAI(), breathMove));

                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                    if (!npc.CanTakeTurnActions())
                    {
                        Debug.Log($"🔥 [AI][Dragon] {npc.Stats.CharacterName} killed during breath positioning move — turn ended");
                        yield break;
                    }
                    if (breathMove.Refused)
                        yield break; // refused by the shared movement legality (logged): not in the breath position

                    npc.Actions.UseMoveAction();
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐉",
                        $"{npc.Stats.CharacterName} maneuvers into optimal breath weapon position!"));
                    yield return new WaitForSeconds(0.4f);
                }
                else if (bp.RequiresMove && !npc.Actions.HasMoveAction)
                {
                    // Can't move — fall back to evaluating breath from current position only
                    Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName} wants to reposition but has no move action — using current position");
                }

                // ── Step 2: Fire breath weapon ──
                // Re-evaluate from actual current position (may have moved or stayed)
                bool breathReady = dragonProfile.EvaluateBreathWeapon(npc, allCombatants, _gameManager);
                if (breathReady && dragonProfile.BreathWeaponAimTarget != null)
                {
                    Debug.Log($"🐉 [AI][Dragon] {npc.Stats.CharacterName} FIRES breath weapon from {npc.GridPosition} " +
                              $"aimed at {dragonProfile.BreathWeaponAimTarget.Stats.CharacterName} " +
                              $"(hits {dragonProfile.BreathWeaponExpectedHits} enemies)!");

                    yield return _gameManager.StartCoroutine(
                        _gameManager.NPCExecuteBreathWeaponForAI(npc, dragonProfile.BreathWeaponAimTarget));
                    if (CombatEnded()) yield break; // the breath ran the combat-end check

                    // After breath weapon, dragon may still have move action for repositioning
                    if (npc.CanTakeTurnActions() && npc.Actions.HasMoveAction)
                    {
                        CharacterController closestEnemy = SelectBestTarget(npc, allCombatants);
                        if (closestEnemy != null)
                        {
                            int dist = SquareGridUtils.GetDistance(npc.GridPosition, closestEnemy.GridPosition);
                            if (dist <= 1)
                            {
                                SquareCell retreatCell = EvaluateMovementOptions(npc, closestEnemy.GridPosition, retreat: true, profile: dragonProfile);
                                if (retreatCell != null && retreatCell.Coords != npc.GridPosition)
                                {
                                    var repositionMove = new MoveAttempt();
                                    yield return _gameManager.StartCoroutine(
                                        _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), repositionMove));
                                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                                    if (!npc.CanTakeTurnActions() || repositionMove.Refused)
                                        yield break; // dropped while moving (CMB-073), or refused (logged): nothing spent
                                    npc.Actions.UseMoveAction();
                                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐉",
                                        $"{npc.Stats.CharacterName} repositions after breath weapon."));
                                    yield return new WaitForSeconds(0.3f);
                                }
                            }
                        }
                    }

                    yield break;
                }
                else
                {
                    Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName} — breath evaluation failed after move " +
                              $"(breathReady={breathReady}, aimTarget={dragonProfile.BreathWeaponAimTarget?.Stats?.CharacterName ?? "null"})");
                    // Fall through to melee — the move was already used for positioning
                }
            }
            else
            {
                // No viable breath position found — try from current position as fallback
                bool breathReady = dragonProfile.EvaluateBreathWeapon(npc, allCombatants, _gameManager);
                if (breathReady && dragonProfile.BreathWeaponAimTarget != null)
                {
                    Debug.Log($"🐉 [AI][Dragon] {npc.Stats.CharacterName} uses breath weapon from current position " +
                              $"(hits {dragonProfile.BreathWeaponExpectedHits} enemies)!");
                    yield return _gameManager.StartCoroutine(
                        _gameManager.NPCExecuteBreathWeaponForAI(npc, dragonProfile.BreathWeaponAimTarget));
                    if (CombatEnded()) yield break; // the breath ran the combat-end check

                    if (npc.CanTakeTurnActions() && npc.Actions.HasMoveAction)
                    {
                        CharacterController closestEnemy = SelectBestTarget(npc, allCombatants);
                        if (closestEnemy != null)
                        {
                            int dist = SquareGridUtils.GetDistance(npc.GridPosition, closestEnemy.GridPosition);
                            if (dist <= 1)
                            {
                                SquareCell retreatCell = EvaluateMovementOptions(npc, closestEnemy.GridPosition, retreat: true, profile: dragonProfile);
                                if (retreatCell != null && retreatCell.Coords != npc.GridPosition)
                                {
                                    var repositionMove = new MoveAttempt();
                                    yield return _gameManager.StartCoroutine(
                                        _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), repositionMove));
                                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                                    if (!npc.CanTakeTurnActions() || repositionMove.Refused)
                                        yield break; // dropped while moving (CMB-073), or refused (logged): nothing spent
                                    npc.Actions.UseMoveAction();
                                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐉",
                                        $"{npc.Stats.CharacterName} repositions after breath weapon."));
                                    yield return new WaitForSeconds(0.3f);
                                }
                            }
                        }
                    }

                    yield break;
                }
            }

            Debug.Log($"🐉 [AI][Dragon] {npc.Stats.CharacterName} — breath weapon available but no viable targets. Proceeding to melee.");
        }

        // ── Priority 3 & 4 & 6: Movement + melee attack ──
        // Check for charge opportunity first
        AIActionType action = SelectBestAction(npc, target, preferAggression: true);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target, ShouldNPCChargeBullRush(npc, target)));
            yield break;
        }

        // Move toward target if not in range (priority 3: movement evaluator respects AoO avoidance).
        // Needs the move action (AI-034): a breath-positioning move already spent it.
        if (!npc.IsTargetInCurrentWeaponRange(target) && npc.Actions.HasMoveAction)
        {
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, dragonProfile);
            if (bestCell != null)
            {
                var advanceMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), advanceMove));

                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI][Dragon] {npc.Stats.CharacterName} killed during movement — turn ended");
                    yield break;
                }

                if (!advanceMove.Refused) // a refused move (logged) spends nothing; the routine goes on
                {
                    npc.Actions.UseMoveAction();
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!"));
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
            yield break;

        // Re-evaluate target after movement (priority 4: profile ScoreTarget already weights healers/mages), among the
        // targets the attack guard allows (batch 13 step 5, D1).
        CharacterController attackTarget = SelectGuardPassingTarget(npc, allCombatants, allowThrow: false);
        target = attackTarget ?? SelectBestTarget(npc, allCombatants);
        if (target == null)
            yield break;

        // ── Priority 6: Full melee attack ──
        if (attackTarget != null && !target.Stats.IsDead)
        {
            // Each melee attack step may be replaced by a maneuver (CMB-102).
            yield return _gameManager.StartCoroutine(PerformMeleeAttackActionWithManeuvers(npc, target, dragonProfile));
        }
        else
        {
            yield return _gameManager.StartCoroutine(TryAIWallInteraction(npc, target));
        }
    }

    private IEnumerator ExecuteRangedKiterTurn(CharacterController npc)
    {
        // Death/disable check: NPC may have been killed by damage before this method runs
        if (npc == null || npc.Stats == null || !npc.CanTakeTurnActions())
        {
            if (npc != null && npc.Stats != null)
                Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} cannot act (HP={npc.Stats.CurrentHP}) at start of ranged kiter turn — turn ended");
            yield break;
        }

        AIProfile profile = GetProfile(npc);
        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();

        CharacterController closestPC = SelectBestTarget(npc, allCombatants);
        if (closestPC == null)
            yield break;

        bool preferRangedVisibility = npc.IsEquippedWeaponRanged();
        LastKnownPositionTracker tracker = LastKnownPositionTracker.Ensure(npc);

        if (npc.CanSee(closestPC, incomingIsRangedAttack: preferRangedVisibility))
        {
            tracker.UpdateLastKnownPosition(closestPC);
        }

        if (!npc.CanSee(closestPC, incomingIsRangedAttack: preferRangedVisibility))
        {
            // The turn-start check (PerceptionRules.ProcessTurnStart) made this turn's one Listen check; read its result
            // (DMG p.295: one check per round; AI-007).
            // The blind attack is declared only when the attack guard allows it (D1: reach, range, ammunition); otherwise the
            // routine searches by moving, below.
            Vector2Int? lastKnown = tracker.GetLastKnownPosition(closestPC);
            bool blindAttackAllowed = AiAttackGuard.Passes(npc, closestPC);
            if (blindAttackAllowed && tracker.IsPinpointedThisRound(closestPC))
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} pinpoints {closestPC.Stats.CharacterName} by sound and attacks their current position."));
            else if (blindAttackAllowed && lastKnown.HasValue)
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{npc.Stats.CharacterName} cannot see {closestPC.Stats.CharacterName} clearly and fires at the last known position."));

            if (blindAttackAllowed && (tracker.IsPinpointedThisRound(closestPC) || lastKnown.HasValue))
            {
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, closestPC));
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            if (npc.Actions.HasMoveAction && MovementService.CanStartMovement(npc, MoveKind.Move).Ok)
            {
                SquareCell blindSearchCell = EvaluateMovementOptions(npc, closestPC.GridPosition, retreat: false, closestPC, profile);
                if (blindSearchCell != null && blindSearchCell.Coords != npc.GridPosition)
                {
                    var blindMove = new MoveAttempt();
                    yield return _gameManager.StartCoroutine(
                        _gameManager.MoveCharacterAlongComputedPathForAI(npc, blindSearchCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), blindMove));
                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                    if (!npc.CanTakeTurnActions() || blindMove.Refused)
                        yield break; // dropped while moving (CMB-073), or refused (logged): nothing spent
                    npc.Actions.UseMoveAction();
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances, trying to reacquire line of sight through concealment."));
                    yield return new WaitForSeconds(0.4f);
                }
            }

            yield break;
        }

        if (TryExecuteSpellcastAction(npc, closestPC))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        // A ranged weapon that cannot shoot (no ammunition, a crossbow it cannot reload in time; the attack guard, D1) fires
        // nothing that could provoke, so no tactical step for it (AI-039, AI-073).
        bool avoidAoORisk = npc.IsEquippedWeaponRanged() && AiAttackGuard.CheckWeapon(npc, null).Ok;
        bool riskIsTooHigh = false;
        bool tookTacticalStep = false;

        if (avoidAoORisk)
        {
            RangedAoORiskAssessment riskAssessment = AssessRangedAoORisk(npc, closestPC, profile, allCombatants);
            riskIsTooHigh = riskAssessment.IsThreatened && riskAssessment.ExpectedDamage > riskAssessment.RiskTolerance;

            if (riskAssessment.IsThreatened)
            {
                if (riskIsTooHigh && TryTakeTacticalFiveFootStep(npc, closestPC, profile, allCombatants, out Vector2Int stepDestination))
                {
                    tookTacticalStep = true;
                    riskIsTooHigh = false;
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Damage("", $"{npc.Stats.CharacterName} takes a tactical 5-foot step to avoid incoming attacks before firing."));
                    Debug.Log($"[AI][RangedAoO] {npc.Stats.CharacterName} 5-foot steps to {stepDestination} (expected={riskAssessment.ExpectedDamage:F1}, tolerance={riskAssessment.RiskTolerance:F1})");
                    yield return new WaitForSeconds(0.35f);
                }
                else
                {
                    string riskLabel = riskIsTooHigh ? "high" : "acceptable";
                    Debug.Log($"[AI][RangedAoO] {npc.Stats.CharacterName} threat risk is {riskLabel} (expected={riskAssessment.ExpectedDamage:F1}, tolerance={riskAssessment.RiskTolerance:F1}, threats={riskAssessment.ThreatCount})");
                }
            }
        }

        int distToClosestPC = SquareGridUtils.GetDistance(npc.GridPosition, closestPC.GridPosition);
        // No movement after a 5-foot step (PHB p.144): the shared legality refuses it (MovementService.CanStartMovement).
        bool canMoveNow = npc.Actions.HasMoveAction && MovementService.CanStartMovement(npc, MoveKind.Move).Ok;
        bool shouldRetreatForDistance = distToClosestPC <= 2 && canMoveNow && !tookTacticalStep;
        bool shouldRetreatForRisk = avoidAoORisk && riskIsTooHigh && canMoveNow && !tookTacticalStep;
        // AI-039 (batch 13 step 5): a kiter backs off only when it can still fight from range: a ranged attack the guard
        // allows on some enemy (a bow with arrows, a thrown weapon), or a castable spell with range beyond touch. Without
        // either it stays and fights with what it has (the attack phase below, or its profile's alternatives).
        if ((shouldRetreatForDistance || shouldRetreatForRisk) && !CanFightFromRange(npc, closestPC, allCombatants))
        {
            Debug.Log($"[AI][Kiter] {npc.Stats.CharacterName} does not retreat: no ranged attack or ranged spell it can use (AI-039)");
            shouldRetreatForDistance = false;
            shouldRetreatForRisk = false;
        }

        if (shouldRetreatForDistance || shouldRetreatForRisk)
        {
            SquareCell retreatCell = EvaluateMovementOptions(npc, closestPC.GridPosition, retreat: true, profile: profile);
            if (retreatCell != null)
            {
                var retreatMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), retreatMove));

                // ── Death/disable check after retreat movement ──
                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dropped during retreat movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!retreatMove.Refused) // a refused move (logged) spends nothing; the routine goes on
                {
                    npc.Actions.UseMoveAction();

                    if (shouldRetreatForRisk)
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} repositions to avoid provoking attacks of opportunity."));
                    else
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} retreats to maintain distance!"));

                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} cannot act before ranged attack phase (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        List<CharacterController> attackPool = _gameManager.GetAllCharactersForAI();
        CharacterController rangedTarget = SelectBestTarget(npc, attackPool);
        if (rangedTarget == null)
            yield break;

        if (TryExecuteSpellcastAction(npc, rangedTarget))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        // The weapon attack goes only to a target the attack guard allows (batch 13 step 5, D1; AI-075): the reach or range
        // of the weapon in hand, its ammunition and load. A spell's range no longer widens the weapon's (the AI-075 cause:
        // GetBestAvailableSpellRangeInSquares let a staff "reach" the spell's range, and the attack then stopped with
        // "target is out of reach" while a foe stood next to the caster).
        CharacterController attackTarget = SelectGuardPassingTarget(npc, attackPool);
        if (attackTarget != null)
        {
            bool usedSpecial = ShouldUseManeuver(npc, attackTarget) && TryExecutePreferredManeuver(npc, attackTarget, profile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, attackTarget));
            else
            {
                // A failed trip may be waiting on a controllable defender's counter-trip choice (CMB-079).
                while (_gameManager.IsAwaitingCounterTripChoice)
                    yield return null;
                yield return new WaitForSeconds(0.8f);
            }
            yield break;
        }

        // No target in reach or range: the profile's alternatives (C1) through the catalog: another target, the preferred
        // range or an approach (then the attack with the standard action left, AI-045), or total defense.
        yield return _gameManager.StartCoroutine(RunOutOfReachAlternatives(npc, rangedTarget, CombatStyle.Ranged, allowThrow: true));
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>
    /// AI-039: whether a kiter can still fight from range after backing off: a ranged attack the attack guard allows on
    /// some enemy (AiAttackGuard.HasRangedAttackOnAnyEnemy: a bow with arrows, a loaded or reloadable crossbow, a thrown
    /// weapon), or a castable spell whose range is beyond touch (the AI's spell pick for <paramref name="target"/>).
    /// </summary>
    internal bool CanFightFromRange(CharacterController npc, CharacterController target, List<CharacterController> allCombatants)
    {
        if (AiAttackGuard.HasRangedAttackOnAnyEnemy(npc, allCombatants))
            return true;
        return HasCastableRangedSpell(npc, target);
    }

    /// <summary>
    /// Whether <paramref name="npc"/> has any ranged means at all, wherever its foes stand: a ranged weapon in hand that can
    /// shoot (ammunition, a loaded or reloadable crossbow: AiAttackGuard.CheckWeapon), a throwable melee weapon (PHB
    /// p.113), or a castable spell with range beyond touch. AiOutOfReach asks it before a move to the preferred range: a
    /// creature with no ranged means does not back off or walk to a range it cannot use (AI-039, AI-073).
    /// </summary>
    internal bool HasRangedMeans(CharacterController npc, CharacterController target)
    {
        if (npc == null || npc.Stats == null)
            return false;
        if (AIProfile.HasUsableRangedWeapon(npc))
            return true;
        return HasCastableRangedSpell(npc, target);
    }

    private bool HasCastableRangedSpell(CharacterController npc, CharacterController target)
        => npc.Stats != null && npc.Stats.IsSpellcaster && GetBestAvailableSpellRangeInSquares(npc, target) > 1;

    private struct RangedAoORiskAssessment
    {
        public bool IsThreatened;
        public int ThreatCount;
        public float ExpectedDamage;
        public float RiskTolerance;
    }

    private RangedAoORiskAssessment AssessRangedAoORisk(
        CharacterController npc,
        CharacterController target,
        AIProfile profile,
        List<CharacterController> allCombatants)
    {
        var assessment = new RangedAoORiskAssessment();
        if (npc == null || npc.Stats == null)
            return assessment;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(npc.GridPosition, npc, allCombatants);
        threateningEnemies.RemoveAll(enemy => !ThreatSystem.CanMakeAoOAgainst(enemy, npc));

        assessment.ThreatCount = threateningEnemies.Count;
        assessment.IsThreatened = assessment.ThreatCount > 0;

        if (!assessment.IsThreatened)
            return assessment;

        assessment.ExpectedDamage = ThreatSystem.CalculateExpectedAoODamageForRangedAttack(npc, threateningEnemies);
        assessment.RiskTolerance = CalculateRangedRiskTolerance(npc, target, profile);
        return assessment;
    }

    private float CalculateRangedRiskTolerance(CharacterController npc, CharacterController target, AIProfile profile)
    {
        if (npc == null || npc.Stats == null)
            return 0f;

        float maxHP = Mathf.Max(1f, npc.Stats.TotalMaxHP);
        float hpPercent = Mathf.Clamp01((float)npc.Stats.CurrentHP / maxHP);

        float tolerancePercent;
        if (hpPercent > 0.75f)
            tolerancePercent = 0.25f;
        else if (hpPercent > 0.5f)
            tolerancePercent = 0.10f;
        else
            tolerancePercent = 0.05f;

        if (profile != null)
            tolerancePercent *= Mathf.Clamp(profile.GetRangedAoORiskToleranceMultiplier(), 0.25f, 2f);

        // Accept slightly higher risk for kill opportunities/high-value enemy casters.
        if (target != null && target.Stats != null)
        {
            bool targetNearDefeat = target.Stats.TotalMaxHP > 0
                && ((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP) <= 0.25f;
            bool highValueTarget = target.Stats.IsWizard || target.Stats.IsCleric;

            if (targetNearDefeat || highValueTarget)
                tolerancePercent += 0.05f;
        }

        return maxHP * Mathf.Clamp(tolerancePercent, 0.02f, 0.35f);
    }

    private bool TryTakeTacticalFiveFootStep(
        CharacterController npc,
        CharacterController target,
        AIProfile profile,
        List<CharacterController> allCombatants,
        out Vector2Int destination)
    {
        destination = npc != null ? npc.GridPosition : Vector2Int.zero;

        if (npc == null || target == null || _gameManager == null || _gameManager.Grid == null)
            return false;

        if (!_gameManager.CanTakeFiveFootStepForAI(npc))
            return false;

        int preferredRange = profile != null && profile.Movement != null
            ? Mathf.Max(1, profile.Movement.PreferredRangeSquares)
            : 4;
        int maxRange = GetMaximumAttackRangeInSquares(npc);

        Vector2Int bestCell = npc.GridPosition;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(npc.GridPosition);
        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int candidate = neighbors[i];
            if (!_gameManager.CanTakeFiveFootStepToForAI(npc, candidate))
                continue;

            int distToTarget = SquareGridUtils.GetDistance(candidate, target.GridPosition);
            if (distToTarget > maxRange)
                continue;

            List<CharacterController> threatsAfterStep = ThreatSystem.GetThreateningEnemies(candidate, npc, allCombatants);
            threatsAfterStep.RemoveAll(enemy => !ThreatSystem.CanMakeAoOAgainst(enemy, npc));

            float expectedAfterStep = ThreatSystem.CalculateExpectedAoODamageForRangedAttack(npc, threatsAfterStep);
            float rangeScore = -Mathf.Abs(distToTarget - preferredRange);
            float threatScore = -expectedAfterStep * 3f;
            float totalScore = threatScore + rangeScore;

            if (threatsAfterStep.Count == 0)
                totalScore += 6f;

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestCell = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        if (_gameManager.TryTakeFiveFootStepForAI(npc, bestCell))
        {
            destination = bestCell;
            return true;
        }

        return false;
    }

    private IEnumerator ExecuteDefensiveMeleeTurn(CharacterController npc, CharacterController preferredTarget)
    {
        CharacterController weakerPC = SelectLowestHPEnemy(npc);
        CharacterController target = weakerPC != null ? weakerPC : preferredTarget;

        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        // Death/disable check: NPC may have been killed by damage before this method runs
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} cannot act (HP={npc.Stats.CurrentHP}) at start of defensive melee turn — turn ended");
            yield break;
        }

        AIActionType action = SelectBestAction(npc, target, preferAggression: false);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target, ShouldNPCChargeBullRush(npc, target)));
            yield break;
        }

        AIProfile profile = GetProfile(npc);

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        // The withdraw it may take: a full-round withdraw, or a Restricted Withdraw (a standard action at speed) when it is
        // limited to a single action, never blinded or after a 5-foot step (PHB p.143-144; the shared legality, GRID-022).
        Legality withdrawLegality = hpPercent < 0.30f
            ? MovementService.CanStartMovement(npc, MovementService.WithdrawKindFor(npc))
            : Legality.Refused("not hurt enough");
        if (hpPercent < 0.30f && !withdrawLegality.Ok)
            Debug.Log($"[AI] {npc.Stats.CharacterName} withdraw refused: {withdrawLegality.Reason}");
        if (withdrawLegality.Ok)
        {
            SquareCell withdrawCell = EvaluateWithdrawRetreatDestination(npc, target.GridPosition);
            if (withdrawCell != null && withdrawCell.Coords != npc.GridPosition)
            {
                var withdrawAttempt = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.ExecuteWithdrawMovementForAI(npc, withdrawCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), withdrawAttempt));

                // ── Death/disable check after withdraw movement ──
                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dropped during withdraw (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                // A withdraw the executor refused (no path, or the shared legality) leaves the turn to the rest of the routine.
                if (withdrawAttempt.Started)
                {
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} withdraws from {target.Stats.CharacterName}."));
                    yield return new WaitForSeconds(0.45f);
                    yield break;
                }
            }
        }

        // Moving needs the move action (AI-034).
        bool triedApproach = false;
        if (!npc.IsTargetInCurrentWeaponRange(target) && npc.Actions.HasMoveAction)
        {
            triedApproach = true;
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                var advanceMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), advanceMove));

                // ── Death/disable check after movement ──
                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dropped during movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!advanceMove.Refused) // a refused move (logged) spends nothing; the routine goes on
                {
                    npc.Actions.UseMoveAction();
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances methodically toward {target.Stats.CharacterName}."));
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} cannot act before attack phase (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        // The attack target among the targets the attack guard allows (batch 13 step 5, D1).
        List<CharacterController> attackPool = _gameManager.GetAllCharactersForAI();
        CharacterController attackTarget = SelectGuardPassingTarget(npc, attackPool, allowThrow: false);
        target = attackTarget ?? SelectBestTarget(npc, attackPool);
        if (target == null)
            yield break;

        if (attackTarget != null && !target.Stats.IsDead)
        {
            // Each melee attack step may be replaced by a maneuver (CMB-102).
            yield return _gameManager.StartCoroutine(PerformMeleeAttackActionWithManeuvers(npc, target, profile));
        }
        else
        {
            // ── AI: Wall of Ice interaction (defensive variant) ──
            yield return _gameManager.StartCoroutine(TryAIWallInteraction(npc, target));

            // A routine that did not try to close in: the profile's alternatives (C1).
            if (!triedApproach && !CombatEnded() && npc.CanTakeTurnActions())
                yield return _gameManager.StartCoroutine(RunOutOfReachAlternatives(npc, target, CombatStyle.Melee, allowThrow: false));
        }
    }

    private IEnumerator ExecuteSwarmTurn(CharacterController swarm, SwarmAI profile, bool indiscriminate)
    {
        if (_gameManager == null || swarm == null || swarm.Stats == null || profile == null)
            yield break;

        List<CharacterController> candidates = BuildSwarmTargetCandidates(swarm, indiscriminate);
        CharacterController target = profile.ResolveTarget(swarm, candidates);

        if (target == null)
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{swarm.Stats.CharacterName} finds no living creatures to swarm."));
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{swarm.Stats.CharacterName} scans for nearest creature: {target.Stats.CharacterName}."));

        // Indiscriminate swarms (Summon Swarm) attack nearest creature regardless of team
        if (indiscriminate && TeamUtility.IsAlly(swarm, target))
        {
            CharacterController summonCaster = _gameManager.GetSummonCasterForAI(swarm);
            if (summonCaster != null && target == summonCaster)
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{swarm.Stats.CharacterName} is uncontrolled and attacks its summoner {target.Stats.CharacterName}!"));
            else
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{swarm.Stats.CharacterName} is uncontrolled and attacks ally {target.Stats.CharacterName}!"));
        }

        if (!swarm.IsTargetInCurrentWeaponRange(target) && swarm.Actions.HasMoveAction)
        {
            var swarmMove = new MoveAttempt();
            yield return _gameManager.StartCoroutine(
                _gameManager.MoveCharacterAlongComputedPathForAI(swarm, target.GridPosition, _gameManager.GetPlayerMoveSecondsPerStepForAI(), swarmMove));

            // ── Death/disable check after swarm movement ──
            if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
            if (!swarm.CanTakeTurnActions())
            {
                Debug.Log($"🔥 [AI] {swarm.Stats.CharacterName} dropped during swarm movement (HP={swarm.Stats.CurrentHP}) — turn ended");
                yield break;
            }

            if (!swarmMove.Refused) // a refused move (logged) spends nothing
            {
                swarm.Actions.UseMoveAction();
                yield return new WaitForSeconds(0.35f);
            }
        }

        // ── Death/disable re-check before swarm attack phase ──
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!swarm.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {swarm.Stats.CharacterName} cannot act before swarm attack phase (HP={swarm.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        target = profile.ResolveTarget(swarm, candidates);
        if (target == null)
            yield break;

        if (swarm.IsTargetInCurrentWeaponRange(target))
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{swarm.Stats.CharacterName} occupies {target.Stats.CharacterName}'s space and continues swarming."));
        }
        else
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{swarm.Stats.CharacterName} shuffles toward {target.Stats.CharacterName}."));
        }

        yield return new WaitForSeconds(0.3f);

        // ── Swarm automatic damage (MM p.239): At the end of the swarm's turn,
        // any creature whose space the swarm occupies takes swarm damage automatically
        // (no attack roll). Then each damaged creature must make a Fortitude save
        // vs the distraction DC or become nauseated for 1 round.
        yield return _gameManager.StartCoroutine(ApplySwarmDamageToOccupants(swarm, indiscriminate));
    }

    /// <summary>
    /// D&D 3.5e swarm damage: automatically deals damage to all creatures occupying the
    /// swarm's space at the end of its turn. No attack roll required.
    /// Also triggers distraction (Fort save or nauseated 1 round).
    /// </summary>
    private IEnumerator ApplySwarmDamageToOccupants(CharacterController swarm, bool indiscriminate)
    {
        if (swarm == null || swarm.Stats == null || !swarm.Stats.IsSwarm)
            yield break;

        SwarmTraits traits = swarm.Stats.SwarmTraits;
        if (traits == null || !traits.IsSwarm)
            yield break;

        Vector2Int swarmPos = swarm.GridPosition;
        List<CharacterController> allChars = _gameManager.GetAllCharactersForAI();
        if (allChars == null)
            yield break;

        // The swarm's damage resolves for every creature in its space first: a reaction to it (the acid spray, D1) comes
        // once all of it has resolved.
        _gameManager.DeferAttackedReactions();
        try
        {
            for (int i = 0; i < allChars.Count; i++)
            {
                CharacterController victim = allChars[i];
                if (victim == null || victim == swarm || victim.Stats == null || victim.Stats.IsDead)
                    continue;

                // Swarms only damage creatures sharing their space
                if (victim.GridPosition != swarmPos)
                    continue;

                // Skip friendly creatures unless this is an indiscriminate swarm
                if (!indiscriminate && !TeamUtility.IsEnemy(swarm, victim))
                    continue;

                // ── Roll swarm damage ──
                int damage = RollSwarmDamage(traits);
                if (damage <= 0)
                    continue;

                // A swarm attack is a nonmagical natural attack: damage reduction applies unless the swarm's damage type
                // bypasses it (MM p.316), and so do immunity and resistance; then the shared concentration and death
                // checks (SPL-004, AI-006).
                string dmgTypeStr = traits.SwarmDamageType.ToString();
                DamagePacket swarmPacket = DamagePackets.CreatureAttack(swarm.Stats.CharacterName + " swarm", traits.SwarmDamageType);
                swarmPacket.SourceCreature = swarm;
                DamageResolutionResult dealt = _gameManager.DealDamage(victim, damage, swarmPacket);
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("", $"🐝 {swarm.Stats.CharacterName} swarm damage: {dealt.FinalDamage} {dmgTypeStr} damage to {victim.Stats.CharacterName}! (no attack roll){GameManager.DescribeMitigation(dealt)}"));

                yield return new WaitForSeconds(0.25f);

                // Check if victim died from swarm damage
                if (victim.Stats.IsDead)
                {
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{victim.Stats.CharacterName} is killed by the swarm!"));
                    continue;
                }

                // Distraction does not yet check MM p.316's "vulnerable to the swarm's damage" (immune, incorporeal, or
                // damage reduction that stops the swarm's maximum) or living creatures only (CRE-060).

                // ── Distraction: Fort save or nauseated 1 round (MM p.239) ──
                if (traits.DistractionDC > 0)
                {
                    var saveResult = SavingThrowResolver.ResolveFortitudeSave(
                        victim.Stats, traits.DistractionDC, $"{swarm.Stats.CharacterName} distraction",
                        SaveContext.CreatureAbility($"{swarm.Stats.CharacterName} distraction", swarm));

                    if (saveResult.Succeeded)
                    {
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Success("💪", $"{victim.Stats.CharacterName} resists distraction (Fort {saveResult.Total} vs DC {traits.DistractionDC})."));
                    }
                    else
                    {
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"🤢 {victim.Stats.CharacterName} fails distraction save (Fort {saveResult.Total} vs DC {traits.DistractionDC}) — nauseated for 1 round!"));
                        victim.ApplyNauseatedCondition(1, $"{swarm.Stats.CharacterName} Distraction");
                    }
                    yield return new WaitForSeconds(0.2f);
                }

                // ── Poison rider (if applicable) ──
                if (traits.HasPoison && !string.IsNullOrEmpty(traits.PoisonId))
                {
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Death("☠", $"{victim.Stats.CharacterName} is exposed to {swarm.Stats.CharacterName}'s poison!"));
                    // Poison application handled by existing poison system if available
                }
            }
        }
        finally
        {
            _gameManager.ResumeAttackedReactions();
        }

        _gameManager.EvaluateCombatEnd("AI.SwarmDamage");
    }

    /// <summary>
    /// Rolls swarm damage from SwarmTraits. Uses SwarmDamage as flat if > 0,
    /// otherwise parses and rolls SwarmDamageDice (e.g. "1d6").
    /// </summary>
    private static int RollSwarmDamage(SwarmTraits traits)
    {
        // If a flat SwarmDamage value is set, use it directly (some swarms have fixed damage)
        if (traits.SwarmDamage > 0)
            return traits.SwarmDamage;

        // Parse dice notation (e.g., "1d6", "2d6")
        string dice = traits.SwarmDamageDice;
        if (string.IsNullOrWhiteSpace(dice))
            return 0;

        string trimmed = dice.Trim().ToLowerInvariant();
        int dIdx = trimmed.IndexOf('d');
        if (dIdx <= 0)
        {
            // Flat number
            if (int.TryParse(trimmed, out int flat))
                return flat;
            return 0;
        }

        string leftStr = trimmed.Substring(0, dIdx);
        string rightStr = dIdx + 1 < trimmed.Length ? trimmed.Substring(dIdx + 1) : "";

        // Handle bonus (e.g., "1d6+2")
        int bonus = 0;
        int plusIdx = rightStr.IndexOf('+');
        int minusIdx = rightStr.IndexOf('-');
        if (plusIdx >= 0)
        {
            string bonusStr = rightStr.Substring(plusIdx + 1);
            int.TryParse(bonusStr, out bonus);
            rightStr = rightStr.Substring(0, plusIdx);
        }
        else if (minusIdx >= 0)
        {
            string bonusStr = rightStr.Substring(minusIdx); // includes the minus
            int.TryParse(bonusStr, out bonus);
            rightStr = rightStr.Substring(0, minusIdx);
        }

        if (!int.TryParse(leftStr, out int count) || count <= 0)
            return 0;
        if (!int.TryParse(rightStr, out int sides) || sides <= 0)
            return 0;

        return DiceService.RollMultiple(count, sides, "swarm damage") + bonus;
    }

    private List<CharacterController> BuildSwarmTargetCandidates(CharacterController swarm, bool indiscriminate)
    {
        List<CharacterController> allCombatants = _gameManager != null
            ? _gameManager.GetAllCharactersForAI()
            : null;

        var candidates = new List<CharacterController>();
        if (allCombatants == null)
            return candidates;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate == swarm || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            // Indiscriminate swarms (Summon Swarm) attack ALL creatures including caster
            // No friend/foe distinction - purely distance-based targeting
            if (!indiscriminate && !TeamUtility.IsEnemy(swarm, candidate))
                continue;

            candidates.Add(candidate);
        }

        return candidates;
    }

    private const string ArmorPriorityBehaviorTag = "Uses Armor-Based Targeting";

    private static AIProfile GetProfile(CharacterController npc)
    {
        return npc != null ? npc.aiProfile : null;
    }

    /// <summary>
    /// The AI's target among the living enemies of <paramref name="npc"/> in <paramref name="allCombatants"/> (visible
    /// ones first, then pinpointed and tracked ones), scored by its profile. <paramref name="filter"/>, when given, keeps
    /// only the candidates it accepts (batch 13 step 5, D1: <see cref="SelectGuardPassingTarget"/> passes the attack guard,
    /// so an attack target is chosen among the targets the creature can reach or hit); it is applied after the
    /// last-known-position updates, so a filtered call tracks the same targets as an unfiltered one.
    /// </summary>
    public CharacterController SelectBestTarget(CharacterController npc, List<CharacterController> allCombatants, Func<CharacterController, bool> filter = null)
    {
        if (npc == null || allCombatants == null)
            return null;

        LastKnownPositionTracker tracker = LastKnownPositionTracker.Ensure(npc);

        var visibleTargets = new List<CharacterController>();
        var concealedTrackedTargets = new List<CharacterController>();

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(npc, candidate))
                continue;

            if (ShouldExcludeTargetBecauseOfCharm(npc, candidate))
                continue;

            if (ShouldExcludeTargetBecauseOfFrightenedSource(npc, candidate))
                continue;

            // Sanctuary (PHB p.274) and Hide from Undead (PHB p.241): a prediction, never a roll (AI-005, decision D6). A
            // known failed Sanctuary save against the casting (or a mindless undead's Hide from Undead) excludes the
            // target; an unrolled Sanctuary only scores it lower (GetPerceptionTargetingAdjustment), and the attack's
            // commit rolls the save. Hide from Undead against an intelligent undead is concealment (CanSeeTarget below).
            if (ShouldExcludeTargetBecauseOfSanctuary(npc, candidate))
                continue;

            if (ShouldExcludeTargetBecauseOfHideFromUndead(npc, candidate))
                continue;

            // Otiluke's Resilient Sphere (PHB p.258; CMB-092): nothing passes its boundary.
            if (ResilientSphereAreaEffect.DoesSphereBlockInteraction(npc, candidate))
                continue;

            if (CanSeeTarget(npc, candidate))
            {
                tracker.UpdateLastKnownPosition(candidate);
                if (filter == null || filter(candidate))
                    visibleTargets.Add(candidate);
            }
            else if (tracker.HasLastKnownPosition(candidate) && (filter == null || filter(candidate)))
            {
                concealedTrackedTargets.Add(candidate);
            }
        }

        // D&D 3.5e priority: visible enemies first, concealed enemies second (only if tracked).
        // Note: forgotten targets are not permanently excluded. If they are visible now,
        // they are added to visibleTargets and immediately tracked again above.
        CharacterController visibleTarget = SelectBestTargetFromCandidates(npc, visibleTargets);
        if (visibleTarget != null)
            return visibleTarget;

        if (concealedTrackedTargets.Count > 0)
        {
            // The pinpoints of this turn's one Listen check, made at turn start for every creature
            // (PerceptionRules.ProcessTurnStart, DMG p.295; AI-007): target choice reads them and rolls nothing.
            var pinpointedTargets = new List<CharacterController>();
            for (int i = 0; i < concealedTrackedTargets.Count; i++)
            {
                CharacterController candidate = concealedTrackedTargets[i];
                if (tracker.IsPinpointedThisRound(candidate))
                    pinpointedTargets.Add(candidate);
            }

            CharacterController pinpointedTarget = SelectBestTargetFromCandidates(npc, pinpointedTargets);
            if (pinpointedTarget != null)
                return pinpointedTarget;

            CharacterController trackedTarget = SelectBestTargetFromCandidates(npc, concealedTrackedTargets);
            if (trackedTarget != null)
                return trackedTarget;
        }

        return null;
    }

    /// <summary>
    /// The best target <paramref name="npc"/> may declare a weapon attack on now (batch 13 step 5; D1, AI-075): the
    /// profile's choice (<see cref="SelectBestTarget"/>) among the enemies that pass the attack guard (AiAttackGuard: the
    /// direct-attack legality with reach and range, ammunition, a loaded or reloadable crossbow), or null when none does.
    /// With <paramref name="allowThrow"/> false (the melee routines, C1) a throwable melee weapon counts its melee reach
    /// only (AiAttackGuard.IntentFor), so a melee creature never picks a far foe to throw its weapon at.
    /// </summary>
    public CharacterController SelectGuardPassingTarget(CharacterController npc, List<CharacterController> allCombatants = null, bool allowThrow = true)
    {
        if (npc == null || _gameManager == null)
            return null;
        return SelectBestTarget(npc, allCombatants ?? _gameManager.GetAllCharactersForAI(), c => AiAttackGuard.Passes(npc, c, allowThrow));
    }

    /// <summary>
    /// The out-of-reach alternatives of <paramref name="npc"/>'s profile (AIProfile.GetOutOfReachAlternatives, C1) through
    /// the action catalog (<see cref="AiOutOfReach.Run"/>), when no target passes its attack guard; <paramref name="focus"/>
    /// is the target the routine chose for its moves; <paramref name="styleWithoutProfile"/> picks the default list for a
    /// creature without a profile; <paramref name="allowThrow"/> is the routine's guard (false for the melee routines).
    /// </summary>
    internal IEnumerator RunOutOfReachAlternatives(CharacterController npc, CharacterController focus, CombatStyle styleWithoutProfile, bool allowThrow, AiOutOfReachOutcome outcome = null)
        => AiOutOfReach.Run(_gameManager, this, npc, focus, styleWithoutProfile, allowThrow, outcome);

    private CharacterController SelectBestTargetFromCandidates(CharacterController npc, List<CharacterController> candidateTargets)
    {
        if (npc == null || candidateTargets == null || candidateTargets.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(npc.PriorityTargetName))
        {
            for (int i = 0; i < candidateTargets.Count; i++)
            {
                CharacterController candidate = candidateTargets[i];
                if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                    continue;
                if (candidate.Stats.CharacterName != npc.PriorityTargetName)
                    continue;

                Debug.Log($"[AI][PriorityTarget] {npc.Stats.CharacterName} prioritizes {candidate.Stats.CharacterName}");
                return candidate;
            }
        }

        AIProfile profile = GetProfile(npc);
        if (profile != null)
        {
            CharacterController profiled = SelectBestTargetFromProfile(npc, candidateTargets, profile);
            if (profiled != null)
                return profiled;
        }

        if (UsesArmorPriorityTargeting(npc))
        {
            CharacterController prioritized = SelectBestArmorPriorityTarget(npc, candidateTargets);
            if (prioritized != null)
                return prioritized;
        }

        return SelectBestTargetDefault(npc, candidateTargets);
    }

    private bool CanSeeTarget(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        bool incomingIsRangedAttack = npc.IsEquippedWeaponRanged();
        return npc.CanSee(target, incomingIsRangedAttack);
    }

    private bool ShouldExcludeTargetBecauseOfCharm(CharacterController npc, CharacterController candidate)
    {
        if (_gameManager == null || npc == null || candidate == null)
            return false;

        if (!npc.HasCondition(CombatConditionType.Charmed))
            return false;

        List<ConditionService.ActiveCondition> active = _gameManager.GetActiveConditions(npc);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Charmed)
                continue;

            CharacterController source = condition.Source;
            if (source == null && condition.Data is CharmedConditionData charmData)
                source = charmData.Caster;

            if (source != null && source == candidate)
                return true;

            if (source == null
                && !string.IsNullOrWhiteSpace(condition.SourceName)
                && candidate.Stats != null
                && string.Equals(condition.SourceName, candidate.Stats.CharacterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldExcludeTargetBecauseOfFrightenedSource(CharacterController npc, CharacterController candidate)
    {
        if (_gameManager == null || npc == null || candidate == null)
            return false;

        if (!npc.HasCondition(CombatConditionType.Frightened))
            return false;

        List<ConditionService.ActiveCondition> active = _gameManager.GetActiveConditions(npc);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Frightened)
                continue;

            CharacterController source = condition.Source;
            if (source == null && condition.Data is FrightenedConditionData fearData)
                source = fearData.Caster;

            if (source != null && source == candidate)
                return true;

            if (source == null
                && !string.IsNullOrWhiteSpace(condition.SourceName)
                && candidate.Stats != null
                && string.Equals(condition.SourceName, candidate.Stats.CharacterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sanctuary (PHB p.274), read only: true when this attacker already failed its one save against the warded
    /// creature's casting (WardSaveLedger, decision D6; AI-005). Choosing a target is not an attempt, so nothing is rolled
    /// or logged here; the save is rolled once, at the attack's commit (AttackLegality.CommitWard).
    /// </summary>
    private bool ShouldExcludeTargetBecauseOfSanctuary(CharacterController npc, CharacterController candidate)
    {
        if (npc == null || candidate == null || candidate.Stats == null || !candidate.Stats.SanctuaryActive)
            return false;
        return WardSaveLedger.HasKnownFailure(npc, candidate);
    }

    /// <summary>
    /// Hide from Undead (PHB p.241), read only: true for a mindless undead, which acts as though the warded creature is not
    /// there (WardSaveLedger.Predict). An intelligent undead is not excluded: until it sees through the ward (its one save
    /// per casting, rolled at its turn-start perception step) the creature is unseen by it, and the unseen-target rules
    /// below apply (total concealment, last-known square; D6). A successful save does not end the spell (the old success
    /// branch ended it for everyone, AI-005).
    /// </summary>
    private bool ShouldExcludeTargetBecauseOfHideFromUndead(CharacterController npc, CharacterController candidate)
    {
        if (npc == null || candidate == null || candidate.Stats == null || !candidate.Stats.HideFromUndeadActive)
            return false;
        return WardSaveLedger.HasKnownFailure(npc, candidate);
    }

    private CharacterController SelectBestTargetFromProfile(CharacterController npc, List<CharacterController> allCombatants, AIProfile profile)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        var enemyCandidates = new List<CharacterController>();
        bool hasConsciousEnemy = false;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(npc, candidate))
                continue;

            enemyCandidates.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = profile.ShouldIgnoreUnconsciousTargets(npc) && hasConsciousEnemy;

        for (int i = 0; i < enemyCandidates.Count; i++)
        {
            CharacterController candidate = enemyCandidates[i];
            if (ignoreUnconscious && candidate.IsUnconscious)
                continue;

            float score = profile.ScoreTarget(candidate, npc);
            score += GetPerceptionTargetingAdjustment(npc, candidate, profile);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            bool incomingIsRanged = npc.IsEquippedWeaponRanged();
            int missChance = best.GetMissChance(npc, incomingIsRanged);
            bool canSee = npc.CanSee(best, incomingIsRanged);
            Debug.Log($"[AI][Profile:{profile.ProfileName}] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} score={bestScore:F1} concealment={missChance}% ({GetConcealmentPriorityLabel(missChance, canSee)})");
        }

        return best;
    }

    private CharacterController SelectBestTargetDefault(CharacterController npc, List<CharacterController> allCombatants)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(npc, candidate))
                continue;

            float score = GetTargetPriority(npc, candidate);
            score += GetPerceptionTargetingAdjustment(npc, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            bool incomingIsRanged = npc.IsEquippedWeaponRanged();
            int missChance = best.GetMissChance(npc, incomingIsRanged);
            bool canSee = npc.CanSee(best, incomingIsRanged);
            Debug.Log($"[AI][Default] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} score={bestScore:F1} concealment={missChance}% ({GetConcealmentPriorityLabel(missChance, canSee)})");
        }

        return best;
    }

    private CharacterController SelectBestArmorPriorityTarget(CharacterController npc, List<CharacterController> allCombatants)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        int maxRange = GetMaximumAttackRangeInSquares(npc);

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(npc, candidate))
                continue;

            int distance = SquareGridUtils.GetDistance(npc.GridPosition, candidate.GridPosition);
            if (distance > maxRange)
                continue;

            float armorScore = GetArmorPriorityScore(candidate);
            float distanceBonus = Mathf.Max(0f, maxRange - distance) * 2f;
            float woundedBonus = Mathf.Clamp01(1f - ((float)candidate.Stats.CurrentHP / Mathf.Max(1f, candidate.Stats.TotalMaxHP))) * 1.5f;
            float totalScore = armorScore + distanceBonus + woundedBonus;
            totalScore += GetPerceptionTargetingAdjustment(npc, candidate);

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                best = candidate;
            }
        }

        if (best != null)
        {
            Debug.Log($"[AI][ArmorPriority] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} ({best.GetArmorTag()}) score={bestScore:F1}");
        }

        return best;
    }

    private bool UsesArmorPriorityTargeting(CharacterController npc)
    {
        if (npc == null)
            return false;

        if (npc.Tags != null && npc.Tags.HasTag(ArmorPriorityBehaviorTag))
            return true;

        if (npc.Stats != null && npc.Stats.CreatureTags != null)
        {
            for (int i = 0; i < npc.Stats.CreatureTags.Count; i++)
            {
                if (string.Equals(npc.Stats.CreatureTags[i], ArmorPriorityBehaviorTag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private float GetArmorPriorityScore(CharacterController target)
    {
        if (target == null)
            return 0f;

        if (target.Tags != null)
        {
            if (target.Tags.HasTag("Unarmored")) return 100f;
            if (target.Tags.HasTag("Light Armor")) return 75f;
            if (target.Tags.HasTag("Medium Armor")) return 50f;
            if (target.Tags.HasTag("Heavy Armor")) return 25f;
        }

        string armorTag = target.GetArmorTag();
        if (string.Equals(armorTag, "Unarmored", StringComparison.OrdinalIgnoreCase)) return 100f;
        if (string.Equals(armorTag, "Light Armor", StringComparison.OrdinalIgnoreCase)) return 75f;
        if (string.Equals(armorTag, "Medium Armor", StringComparison.OrdinalIgnoreCase)) return 50f;
        if (string.Equals(armorTag, "Heavy Armor", StringComparison.OrdinalIgnoreCase)) return 25f;

        return 10f;
    }

    private float GetPerceptionTargetingAdjustment(CharacterController npc, CharacterController target, AIProfile profile = null)
    {
        if (npc == null || target == null || target.Stats == null)
            return 0f;

        // A ward this attacker has not saved against yet (Sanctuary, Hide from Undead): the commit may lose the attack, so
        // the target ranks lower (AI-005, D6).
        float adjustment = AttackLegality.WardTargetScoreAdjustment(npc, target);

        if (target.IsInvisibleCondition)
        {
            int distanceSquares = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
            bool hasScent = npc.Stats != null && npc.Stats.HasScent;

            if (hasScent)
            {
                // Scent gives a strong close-range lock and partial tracking at longer range.
                adjustment += distanceSquares <= 6 ? 4f : -6f;
            }
            else
            {
                // Without scent, invisible targets are much harder to prioritize reliably.
                adjustment += -18f;
            }
        }

        bool incomingIsRanged = npc.IsEquippedWeaponRanged();
        npc.UpdateLastKnownPosition(target, incomingIsRanged);

        int missChance = target.GetMissChance(npc, incomingIsRanged);
        bool canSeeTarget = npc.CanSee(target, incomingIsRanged);
        bool hasLastKnownPosition = npc.GetLastKnownPosition(target).HasValue;

        if (!canSeeTarget)
        {
            // Keep baseline visibility awareness so non-concealment blind spots still receive a penalty.
            adjustment += -12f;
            if (hasLastKnownPosition)
                adjustment += 4f;
        }

        if (profile == null || profile.PrioritizeVisibleTargets)
        {
            float concealmentMultiplier = profile != null ? Mathf.Max(0f, profile.ConcealmentPenaltyMultiplier) : 1f;
            adjustment += GetConcealmentTargetingAdjustment(missChance, canSeeTarget, hasLastKnownPosition, concealmentMultiplier);
        }

        return adjustment;
    }

    internal static float GetConcealmentTargetingAdjustment(int missChance, bool canSeeTarget, bool hasLastKnownPosition, float penaltyMultiplier = 1f)
    {
        float multiplier = Mathf.Max(0f, penaltyMultiplier);
        if (multiplier <= 0f)
            return 0f;

        int normalizedMissChance = Mathf.Clamp(missChance, 0, 100);

        if (normalizedMissChance <= 0)
            return 50f * multiplier; // Highest priority: clean line of attack.

        if (normalizedMissChance < 50)
            return -30f * multiplier; // Medium priority: partial concealment.

        float totalConcealmentPenalty = -80f * multiplier; // Low priority by default.
        if (!canSeeTarget)
            totalConcealmentPenalty -= 40f * multiplier; // Lowest priority when target cannot be seen.

        if (hasLastKnownPosition)
            totalConcealmentPenalty += 20f * multiplier; // Slight recovery when a trackable position exists.

        return totalConcealmentPenalty;
    }

    private static string GetConcealmentPriorityLabel(int missChance, bool canSeeTarget)
    {
        int normalizedMissChance = Mathf.Clamp(missChance, 0, 100);
        if (normalizedMissChance <= 0)
            return "visible";
        if (normalizedMissChance < 50)
            return "partially concealed";
        if (canSeeTarget)
            return "totally concealed";

        return "unknown position";
    }

    public float GetTargetPriority(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null)
            return float.MinValue;

        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        float distanceScore = Mathf.Max(0f, 12f - distance) * 1.8f; // proximity

        float threatScore = CalculateThreat(npc, target) * 2.0f; // healers/casters/high offense

        float hpRatio = target.Stats.TotalMaxHP > 0
            ? Mathf.Clamp01((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP)
            : 1f;
        float woundedBonus = (1f - hpRatio) * 8f; // finish wounded enemies

        float armorEase = Mathf.Clamp(26f - target.Stats.ArmorClass, -6f, 8f) * 0.7f; // easier AC is better

        bool flankingOpportunity = CombatUtils.CanThreatenTargetFromPosition(npc, npc.GridPosition, target);
        float flankingBonus = flankingOpportunity ? 2.5f : 0f;

        string creatureType = target.Stats.CreatureType ?? string.Empty;
        bool vulnerableType = creatureType.IndexOf("undead", StringComparison.OrdinalIgnoreCase) >= 0
            || creatureType.IndexOf("outsider", StringComparison.OrdinalIgnoreCase) >= 0;
        float vulnerabilityBonus = vulnerableType ? 1.5f : 0f;

        return distanceScore + threatScore + woundedBonus + armorEase + flankingBonus + vulnerabilityBonus;
    }

    public float CalculateThreat(CharacterController observer, CharacterController target)
    {
        if (target == null || target.Stats == null)
            return 0f;

        float score = 0f;

        if (target.Stats.IsCleric || target.Stats.IsWizard)
            score += 3f;
        if (target.Stats.IsRogue)
            score += 2f;

        score += Mathf.Clamp(target.Stats.Level * 0.35f, 0f, 4f);
        score += Mathf.Clamp(target.Stats.STRMod + target.Stats.DEXMod, -2f, 6f) * 0.35f;

        if (target.HasCondition(CombatConditionType.Prone))
            score -= 0.75f;

        return score;
    }

    /// <summary>
    /// The best square within one move of <paramref name="mover"/>: farthest from <paramref name="targetPos"/> on a
    /// retreat, else closest to the preferred range (the profile's Movement.PreferredRangeSquares, or
    /// <paramref name="preferredRangeOverride"/>: the approach to the weapon's reach or range, AiOutOfReach), with a bonus
    /// for threatening and flanking <paramref name="targetCharacter"/> and a penalty for paths that provoke.
    /// </summary>
    public SquareCell EvaluateMovementOptions(CharacterController mover, Vector2Int targetPos, bool retreat, CharacterController targetCharacter = null, AIProfile profile = null, int? preferredRangeOverride = null)
    {
        if (mover == null || mover.Stats == null || _gameManager.Grid == null)
            return null;

        if (profile == null)
            profile = GetProfile(mover);

        int moverMoveRange = _gameManager.GetCurrentMoveRangeSquares(mover);
        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(mover.GridPosition, moverMoveRange);
        List<CharacterController> allCombatants = targetCharacter != null ? _gameManager.GetAllCharactersForAI() : null;

        SquareCell bestCell = null;
        float bestScore = float.NegativeInfinity;

        int preferredRange = preferredRangeOverride.HasValue
            ? Mathf.Max(0, preferredRangeOverride.Value)
            : profile != null && profile.Movement != null
                ? Mathf.Max(0, profile.Movement.PreferredRangeSquares)
                : 1;

        bool avoidAoOs = profile != null
            && profile.Movement != null
            && profile.Movement.AvoidAoOs
            && !profile.ShouldIgnoreAoO(mover);
        bool seekFlanking = profile == null || profile.Movement == null || profile.Movement.SeekFlanking;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, mover.GetVisualSquaresOccupied(), mover))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(mover, cell.Coords, avoidThreats: false, maxRangeOverride: moverMoveRange);
            if (pathResult == null || pathResult.Path == null)
                continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, targetPos);
            bool canThreatenFromCell = false;
            bool wouldFlankFromCell = false;
            if (targetCharacter != null)
            {
                canThreatenFromCell = CombatUtils.CanThreatenTargetFromPosition(mover, cell.Coords, targetCharacter);
                if (canThreatenFromCell)
                {
                    CharacterController flankPartner;
                    wouldFlankFromCell = CombatUtils.IsAttackerFlankingFromPosition(
                        mover,
                        cell.Coords,
                        targetCharacter,
                        allCombatants,
                        out flankPartner);
                }
            }

            float score;
            if (retreat)
            {
                score = dist * 2f;
            }
            else
            {
                int distanceToPreferred = Mathf.Abs(dist - preferredRange);
                score = -distanceToPreferred * 2f;
                if (canThreatenFromCell)
                    score += 2f;
                if (seekFlanking && wouldFlankFromCell)
                    score += 3f;
            }

            if (pathResult.ProvokesAoOs)
                score += avoidAoOs ? -1000f : -2f * pathResult.ProvokedAoOs.Count;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    internal SquareCell EvaluateWithdrawRetreatDestination(CharacterController mover, Vector2Int dangerSource)
    {
        if (mover == null || mover.Stats == null || _gameManager.Grid == null)
            return null;

        int withdrawRange = Mathf.Max(0, _gameManager.GetWithdrawMoveRangeSquares(mover)); // speed for a Restricted Withdraw (PHB p.144)
        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(mover.GridPosition, withdrawRange);

        SquareCell bestCell = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, mover.GetVisualSquaresOccupied(), mover))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(
                mover,
                cell.Coords,
                avoidThreats: false,
                maxRangeOverride: withdrawRange,
                allowThroughAllies: true,
                allowThroughEnemies: false,
                suppressFirstSquareAoO: true);

            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            int distance = SquareGridUtils.GetDistance(cell.Coords, dangerSource);
            int provokes = pathResult.ProvokedAoOs != null ? pathResult.ProvokedAoOs.Count : 0;

            float score = distance * 3f;
            score -= provokes * 4f;
            score -= Mathf.Max(0, pathResult.Path.Count - withdrawRange) * 2f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    /// <summary>
    /// The defender's choice after a failed trip against an AI-run <paramref name="defender"/> (PHB p.158,
    /// CMB-079): its profile's <see cref="AIProfile.ShouldCounterTrip"/>, or
    /// <see cref="AIProfile.DefaultShouldCounterTrip"/> without a profile. Asked by
    /// GameManager.HandleTripAftermath only when the rule allows the counter-trip.
    /// </summary>
    public bool ShouldCounterTrip(CharacterController defender, CharacterController tripper)
    {
        AIProfile profile = GetProfile(defender);
        bool tripBack = profile != null
            ? profile.ShouldCounterTrip(defender, tripper)
            : AIProfile.DefaultShouldCounterTrip(defender, tripper);
        Debug.Log($"[AI][CounterTrip] {defender?.Stats?.CharacterName ?? "<null>"} {(tripBack ? "trips back" : "declines to trip back")} against {tripper?.Stats?.CharacterName ?? "<null>"}.");
        return tripBack;
    }

    public bool ShouldUseManeuver(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null)
            return false;

        AIProfile profile = GetProfile(npc);
        if (_gameManager != null
            && _gameManager.CanUseCoupDeGraceAttackOption(npc)
            && ShouldNPCUseCoupDeGrace(npc, profile))
        {
            return true;
        }

        if (npc.IsGrappling())
            return false;
        // Trip, disarm, sunder and grapple need only the next attack step, iterative or natural
        // (CMB-102); TryNPCSpecialAttackIfBeneficial enforces the exact cost of the chosen type.
        if (!npc.Actions.HasStandardAction && !npc.CanCommitAttack(npc.GetManeuverSubstituteStepKind(), out _))
            return false;

        if (profile != null)
        {
            SpecialAttackType? preferred = profile.GetPreferredManeuver(npc, target);
            if (preferred.HasValue)
            {
                if (!npc.CanPerformSpecialAttack(preferred.Value))
                {
                    Debug.Log($"[AI][Maneuver] {npc.Stats.CharacterName} skips {preferred.Value} due to ranged-only loadout ({npc.GetPrimaryWeaponType()}).");
                    return false;
                }

                if (preferred.Value == SpecialAttackType.Trip)
                    return !target.HasCondition(CombatConditionType.Prone) && npc.HasMeleeWeaponEquipped()
                        && npc.CanTrip(target, out _); // size, swarm, incorporeal (PHB p.158, CMB-079)

                if (preferred.Value == SpecialAttackType.Disarm)
                    return target.HasDisarmableWeaponEquipped();

                if (preferred.Value == SpecialAttackType.Grapple)
                    return profile.ShouldInitiateGrapple(npc, target);

                if (preferred.Value == SpecialAttackType.Sunder)
                    // PHB p.158, CMB-102, CMB-141; a sunder dealing nonlethal damage (an unarmed strike without Improved Unarmed Strike,
                    // a weapon in nonlethal mode) cannot harm an object (PHB p.165), so the AI skips it.
                    return target.HasSunderableItemEquipped() && npc.CanSunderWithAttack(-1, out _)
                        && !npc.WouldSunderDealNonlethalDamage();

                return true;
            }

            // Profile present but no preferred maneuver => obey profile and skip legacy fallback.
            return false;
        }

        if (target.GetEquippedMainWeapon() != null
            && npc.Stats.STRMod >= 3
            && npc.CanPerformSpecialAttack(SpecialAttackType.Disarm))
            return true; // disarm preference
        if (!target.Stats.IsProne
            && npc.HasMeleeWeaponEquipped()
            && npc.CanPerformSpecialAttack(SpecialAttackType.Trip)
            && npc.CanTrip(target, out _))
            return true; // trip preference

        return npc.Stats.STRMod >= 4
            && npc.CanPerformSpecialAttack(SpecialAttackType.Grapple);
    }

    /// <summary>
    /// Attack action whose melee steps the AI may each replace with a maneuver (CMB-102): before every
    /// step the usual maneuver evaluation runs (coup de grace, then the profile preference, then the
    /// legacy chooser), limited by the per-turn stopgap in <see cref="TryExecutePreferredManeuver"/>
    /// (AI-060): after a maneuver succeeds the remaining steps are attacks (a trip is followed by
    /// attacks on the prone target), and a failed maneuver type is not retried against that target
    /// that turn. Trip, disarm, sunder and grapple use that step at its BAB
    /// (ManeuverActionCost.ReplacesMeleeAttack); other maneuvers spend their own action and end it.
    /// </summary>
    private IEnumerator PerformMeleeAttackActionWithManeuvers(CharacterController npc, CharacterController target, AIProfile profile)
    {
        // A feint before the attack (PHB p.155; a move action with Improved Feint, PHB p.95; CMB-102): an AI stopgap, by the
        // profile (AIProfile.ShouldFeint; by default a rogue with Improved Feint that makes one attack anyway). The feint is
        // the NPC row of the maneuver core (TryNPCSpecialAttackByTypeForAI, the PC's cost table).
        bool feint = profile != null ? profile.ShouldFeint(npc, target) : AIProfile.DefaultShouldFeint(npc, target);
        if (feint && _gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.Feint))
        {
            if (CombatEnded() || npc.IsDead || !npc.CanTakeTurnActions())
                yield break;
            yield return new WaitForSeconds(0.3f);
        }

        yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(
            npc,
            target,
            CreateMeleeStepManeuverEvaluator(profile)));
    }

    /// <summary>
    /// The per-step maneuver evaluation PerformMeleeAttackActionWithManeuvers hands to the NPC melee
    /// sequence (GameManager.RunAttackSequence through NPCPerformAttack). Internal so the static suites can run the
    /// real profile evaluation through the loop.
    /// </summary>
    internal System.Func<CharacterController, CharacterController, bool> CreateMeleeStepManeuverEvaluator(AIProfile profile)
        => (actor, stepTarget) => ShouldUseManeuver(actor, stepTarget) && TryExecutePreferredManeuver(actor, stepTarget, profile);

    // STOPGAP (owner decision 2026-10-07, CMB-102 open item 7): the maneuver evaluation below re-runs
    // before every attack step, so on its own it chains maneuvers (trip, then disarm, then grapple)
    // and retries failed ones. Until it is replaced by weighted personality scoring in the AI profiles
    // (AI-060; docs/designs/enemy_ai_knowledge_and_personalities.md, weighted personalities step), a
    // per-creature, per-turn memory (CharacterController.AIManeuverMemory, cleared at turn start)
    // limits it: once a maneuver succeeds, the AI makes no further maneuver that turn and attacks
    // with the remaining steps; after a maneuver fails, it does not retry that type against that
    // target that turn (it attacks instead, or uses another maneuver if the evaluation picks one).
    // A grapple that took hold keeps its grapple-action handoff, and coup de grace, the profile
    // preference order and the legacy chooser are unchanged. AI decision only, not a rule.
    private bool TryExecutePreferredManeuver(CharacterController npc, CharacterController target, AIProfile profile)
    {
        if (npc == null || target == null)
            return false;

        if (_gameManager != null
            && _gameManager.CanUseCoupDeGraceAttackOption(npc)
            && ShouldNPCUseCoupDeGrace(npc, profile))
        {
            return _gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.CoupDeGrace);
        }

        if (profile != null)
        {
            SpecialAttackType? preferred = profile.GetPreferredManeuver(npc, target);
            if (preferred.HasValue)
            {
                if (!npc.CanPerformSpecialAttack(preferred.Value))
                {
                    Debug.Log($"[AI][Maneuver] {npc.Stats.CharacterName} blocked from executing {preferred.Value} due to ranged-only loadout ({npc.GetPrimaryWeaponType()}).");
                    return false;
                }

                if (IsManeuverForbiddenByTurnStopgap(npc, target, preferred.Value))
                    return false;

                bool acted = _gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, preferred.Value, out bool succeeded);
                if (acted)
                    npc.AIManeuverMemory.Record(target, preferred.Value, succeeded);
                return acted;
            }

            return false;
        }

        SpecialAttackType? legacyChoice = _gameManager.PeekNPCFallbackManeuverForAI(npc, target);
        if (legacyChoice.HasValue && IsManeuverForbiddenByTurnStopgap(npc, target, legacyChoice.Value))
            return false;

        bool legacyActed = _gameManager.TryNPCSpecialAttackIfBeneficialForAI(npc, target, out SpecialAttackType? attempted, out bool legacySucceeded);
        if (legacyActed && attempted.HasValue)
            npc.AIManeuverMemory.Record(target, attempted.Value, legacySucceeded);
        return legacyActed;
    }

    /// <summary>
    /// The AI-060 stopgap check (see the comment above <see cref="TryExecutePreferredManeuver"/>): true when the
    /// creature's per-turn maneuver memory forbids <paramref name="type"/> against <paramref name="target"/>.
    /// The grapple handoff does not pass through here: after a grapple takes hold,
    /// GameManager.RunAttackSequence stops the step loop and NPCAttackSequence hands the
    /// remaining steps to AI_GrappleRestrictedTurn as grapple actions, and the executor
    /// (TryNPCSpecialAttackIfBeneficial) refuses every maneuver but coup de grace while grappling.
    /// </summary>
    private static bool IsManeuverForbiddenByTurnStopgap(CharacterController npc, CharacterController target, SpecialAttackType type)
    {
        if (npc == null)
            return false;

        if (!npc.AIManeuverMemory.Forbids(target, type, out string reason))
            return false;

        Debug.Log($"[AI][Maneuver] {npc.Stats?.CharacterName ?? "<unknown>"} skips {type} against {target?.Stats?.CharacterName ?? "<null>"}: {reason} (AI-060 stopgap); it attacks instead.");
        return true;
    }

    private static bool ShouldNPCUseCoupDeGrace(CharacterController npc, AIProfile profile)
    {
        if (npc == null)
            return false;

        if (npc.EnemyUseCoupDeGraceOverride.HasValue)
            return npc.EnemyUseCoupDeGraceOverride.Value;

        AIProfile resolvedProfile = profile ?? GetProfile(npc);
        return resolvedProfile != null && resolvedProfile.ShouldUseCoupDeGrace(npc);
    }

    private static bool HasCastablePreparedSpells(CharacterController caster)
    {
        if (caster == null)
            return false;

        SpellcastingComponent spellcasting = caster.Spellcasting;
        return spellcasting != null && spellcasting.CanCastSpells && spellcasting.HasAnyCastablePreparedSpell();
    }

    private int GetBestAvailableSpellRangeInSquares(CharacterController caster, CharacterController target)
    {
        if (caster == null || caster.Stats == null)
            return 0;

        SpellData bestSpell = SelectSpell(caster, target);
        if (bestSpell == null)
            return 0;

        int rangeSquares = AiMetamagicPlanner.RangeSquaresFor(caster, bestSpell);
        return Mathf.Max(1, rangeSquares);
    }

    /// <summary>
    /// The turn's spell (batch 11 step 22): the castable spells ranked by score (<see cref="ScoreCastableSpells"/>, the
    /// order <see cref="SelectSpell"/> picks from), each with its best target among the casts the cast core would let
    /// through (<see cref="SelectBestSpellTarget"/>, GameManager.CheckLegalityForAI), are tried best first; a cast the
    /// NPC cast path refuses (TryNPCPerformSpellCastForAI false: nothing spent) passes to the next candidate, at most
    /// <see cref="AiSpellCandidates.MaxTries"/> tries (AI-063, single-target part). A spell too risky to cast while
    /// threatened (AISpellcastingStrategist.EvaluateDefensiveCasting) or with no legal target is skipped and costs no try.
    /// Since batch 11 step 25 (AI-001 area half) an area spell is a candidate too, aimed at the best placement its aim
    /// search found (AreaAimSearch, through the profile's ally sparing) and cast through the area resolve
    /// (GameManager.TryNPCPerformAreaSpellCastForAI), within the same cap; one with no legal, worthwhile placement is not
    /// a candidate. Since batch 11 step 27 a Summon Monster or Summon Nature's Ally is a candidate too, cast as a summon
    /// whose creature and square the AI decider picks (GameManager.TryNPCPerformSummonSpellCastForAI; SPL-091), Dimension
    /// Door is cast to escape when the caster is in danger, and a hostile melee touch spell whose foes are out of reach is
    /// cast and held when a foe can be reached this turn (GameManager.TryNPCHoldTouchChargeForAI; the move and the touch
    /// close the turn, ExecuteNPCTurn; SPL-115). Imbue with Spell Ability stays out (AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive).
    /// Since batch 11 step 28 (AI-001) a candidate may carry the metamagic a spontaneous caster chose
    /// (AiMetamagicPlanner; SpellData.AiChosenMetamagic), which its legality probes and its cast take with it; the cap is
    /// the same.
    /// </summary>
    /// <param name="allow">Optional filter on the candidate spells (the dragon turn's cast-first step, AI-037); null allows all.</param>
    /// <param name="minScore">A candidate must score above this to be chosen; the default keeps the old no-floor choice.</param>
    private bool TryExecuteSpellcastAction(CharacterController caster, CharacterController fallbackTarget,
        Func<SpellData, bool> allow = null, float minScore = float.NegativeInfinity)
    {
        if (_gameManager == null || caster == null || caster.Stats == null)
            return false;

        if (!caster.Actions.HasStandardAction)
            return false;

        if (!caster.Stats.IsSpellcaster)
            return false;

        SpellcastingComponent spellcasting = caster.Spellcasting;
        List<SpellData> castableSpells = spellcasting != null ? spellcasting.GetCastablePreparedSpells() : null;
        int castableCount = castableSpells != null ? castableSpells.Count : 0;

        List<AiScoredSpell> scored = ScoreCastableSpells(caster, fallbackTarget, allow, minScore, logAreaSkips: true);
        if (scored.Count == 0)
        {
            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} has no castable spell choice. castablePrepared={castableCount}");
            return false;
        }

        bool casted = AiSpellCandidates.TryInOrder(RankedSpellCandidates(caster, fallbackTarget, scored), AiSpellCandidates.MaxTries, candidate =>
        {
            SpellData spell = candidate.Spell;
            if (candidate.Kind == AiSpellCastKind.Summon)
            {
                // A summon (batch 11 step 27, SPL-091): the AI decider picks the creature and its square; the same Summon
                // resolve as a PC's.
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} evaluating {spell.Name} as a summon, castablePrepared={castableCount}");
                if (!_gameManager.TryNPCPerformSummonSpellCastForAI(caster, spell))
                {
                    Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} failed to cast {spell.Name} (refused, nothing spent); trying the next candidate.");
                    return false;
                }
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} casts {spell.Name}");
                return true;
            }

            if (candidate.Kind == AiSpellCastKind.HoldCharge)
            {
                // A melee touch spell at a foe out of reach (batch 11 step 27, SPL-115): cast it and hold the charge, then
                // move into reach and touch at the turn's end (ExecuteNPCTurn), when the foe can be reached this turn.
                CharacterController holdTarget = candidate.Target;
                if (AiCastDecider.TouchDeliveryFor(_gameManager, caster, holdTarget, spell) != CastTouchDelivery.HoldCharge)
                    return false;
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} evaluating {spell.Name} to hold its charge for {holdTarget.Stats.CharacterName}, castablePrepared={castableCount}");
                if (!_gameManager.TryNPCHoldTouchChargeForAI(caster, spell))
                {
                    Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} failed to hold {spell.Name} (refused, nothing spent); trying the next candidate.");
                    return false;
                }
                if (caster.Spellcasting != null && caster.Spellcasting.HasHeldTouchCharge)
                    _heldChargePlans[caster] = holdTarget;
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} holds the charge of {spell.Name} to touch {holdTarget.Stats.CharacterName}");
                return true;
            }

            if (candidate.Placement != null)
            {
                // An area the AI aimed (batch 11 step 25): the same cast core as a PC's area cast.
                AreaPlacement aim = candidate.Placement;
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} evaluating {AiMetamagicPlanner.Describe(spell)} as an area at {aim}, castablePrepared={castableCount}");
                if (!_gameManager.TryNPCPerformAreaSpellCastForAI(caster, spell, aim))
                {
                    Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} failed to cast {spell.Name} at {aim} (refused, nothing spent); trying the next candidate.");
                    return false;
                }
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} casts {AiMetamagicPlanner.Describe(spell)} at {aim}");
                return true;
            }

            CharacterController spellTarget = candidate.Target;
            int targetDistance = CasterLevelRules.DistanceToTarget(caster, spellTarget);
            int spellRange = Mathf.Max(1, AiMetamagicPlanner.RangeSquaresFor(caster, spell));
            bool isAllyTargeted = AISpellcastingStrategist.IsAllyTargetedSpell(spell);
            string targetRelation = isAllyTargeted ? "ally" : "enemy";
            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} evaluating {AiMetamagicPlanner.Describe(spell)}: target={spellTarget.Stats.CharacterName} ({targetRelation}), distance={targetDistance}, range={spellRange}, castablePrepared={castableCount}");

            if (!_gameManager.TryNPCPerformSpellCastForAI(caster, spellTarget, spell))
            {
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} failed to cast {spell.Name} on {spellTarget.Stats.CharacterName} (refused, nothing spent); trying the next candidate.");
                return false;
            }

            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} casts {AiMetamagicPlanner.Describe(spell)} on {spellTarget.Stats.CharacterName}");
            return true;
        }, out int tries);

        if (!casted)
            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} cast no spell ({tries} tried of {scored.Count} scored).");
        return casted;
    }

    /// <summary>
    /// The (spell, target) candidates of <paramref name="scored"/> in rank order, built as the walk asks for them: a spell
    /// too risky to cast while threatened, or with no legal target, is left out (with a log line).
    /// </summary>
    private IEnumerable<AiSpellCandidate> RankedSpellCandidates(CharacterController caster, CharacterController fallbackTarget, List<AiScoredSpell> scored)
    {
        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();
        for (int i = 0; i < scored.Count; i++)
        {
            SpellData spell = scored[i].Spell;

            // ── T1.3: Defensive casting awareness ──
            int defensiveResult = AISpellcastingStrategist.EvaluateDefensiveCasting(caster, spell, allCombatants);
            if (defensiveResult == 0)
            {
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} can't safely cast {spell.Name} while threatened; it looks further down its list.");
                continue;
            }
            if (defensiveResult == 1)
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} will cast {spell.Name} defensively (threatened in melee).");

            // An area spell carries the placement its search found (ScoreCastableSpells; batch 11 step 25).
            if (scored[i].Placement != null)
            {
                yield return new AiSpellCandidate(spell, null, scored[i].Score, scored[i].Placement);
                continue;
            }

            // A summon has no target: the AI decider places its creature (batch 11 step 27).
            if (scored[i].Summon)
            {
                yield return new AiSpellCandidate(spell, null, scored[i].Score, null, AiSpellCastKind.Summon);
                continue;
            }

            CharacterController spellTarget = SelectBestSpellTarget(caster, spell, fallbackTarget);
            if (spellTarget == null)
            {
                // A hostile melee touch spell with every foe out of reach (batch 11 step 27, SPL-115): cast it and hold the
                // charge when a foe can be reached with a move this turn (AiCastDecider.TouchDeliveryFor).
                CharacterController holdTarget = SelectHoldChargeTarget(caster, spell, fallbackTarget);
                if (holdTarget != null)
                {
                    yield return new AiSpellCandidate(spell, holdTarget, scored[i].Score, null, AiSpellCastKind.HoldCharge);
                    continue;
                }

                int rangeSquares = Mathf.Max(1, AiMetamagicPlanner.RangeSquaresFor(caster, spell));
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} selected {spell.Name} but found no legal target ({rangeSquares} squares).");
                continue;
            }

            yield return new AiSpellCandidate(spell, spellTarget, scored[i].Score);
        }
    }

    /// <summary>
    /// The foe a hostile melee touch spell would be held for (batch 11 step 27, SPL-115): a living foe (team allegiance)
    /// the spell may touch but that is out of reach now (the cast core's legality on the NPC cast refuses it only as
    /// "out-of-range"), that the caster can reach with a move this turn (AiCastDecider.TouchDeliveryFor answers
    /// HoldCharge), and for which the hold itself is legal (GameManager.CheckHoldLegalityForAI): <paramref name="preferred"/>
    /// when it qualifies, else the nearest. Null otherwise, and null for a cast that takes the full round (a spontaneous
    /// metamagic candidate, PHB p.88), which leaves no move to approach with.
    /// </summary>
    private CharacterController SelectHoldChargeTarget(CharacterController caster, SpellData spell, CharacterController preferred)
    {
        if (_gameManager == null || caster == null || spell == null || !spell.IsMeleeTouchSpell()
            || !AISpellcastingStrategist.IsEnemyTargetedSpell(spell) || AISpellcastingStrategist.IsAllyTargetedSpell(spell))
            return null;
        if (caster.Actions == null || !caster.Actions.HasMoveAction)
            return null;
        // The cast must leave the move that approaches (PHB p.141: the move comes before, between or after the cast and
        // the touch): a spontaneous metamagic cast takes the full round (PHB p.88), so no hold is planned for it (batch 11
        // step 28 review); the plain spell's candidate still is.
        if (CastActionRules.CastingAction(spell, SpellCastRequest.AiMetamagicFor(spell), SpellCastRequest.AiSourceFor(caster, spell)) == CastActionCost.FullRound)
            return null;
        if (_gameManager.CheckHoldLegalityForAI(caster, spell).Kind == CastOutcomeKind.Refused)
            return null;

        CharacterController best = null;
        int bestDistance = int.MaxValue;
        foreach (CharacterController c in _gameManager.GetAllCharactersForAI())
        {
            if (c == null || c == caster || c.Stats == null || c.Stats.IsDead || !TeamUtility.IsEnemy(caster, c))
                continue;
            // A foe the spell is worth landing on (not an undead an inflict spell would heal; IsWorthwhileSpellTarget).
            if (!AISpellcastingStrategist.IsWorthwhileSpellTarget(caster, spell, c, _gameManager))
                continue;
            CastOutcome legality = _gameManager.CheckLegalityForAI(caster, c, spell);
            if (legality.Kind != CastOutcomeKind.Refused || legality.Reason != "out-of-range")
                continue;
            if (AiCastDecider.TouchDeliveryFor(_gameManager, caster, c, spell) != CastTouchDelivery.HoldCharge)
                continue;
            if (c == preferred)
                return c;
            int distance = CasterLevelRules.DistanceToTarget(caster, c);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = c;
            }
        }
        return best;
    }

    /// <summary>
    /// Delivers <paramref name="npc"/>'s held touch charge (batch 11 step 27, SPL-115; PHB p.141), at the PC's cost (batch 12
    /// step 18, SPL-156: the delivery's legality refuses a touch the creature has no attack or standard action left for);
    /// the target is the creature the charge may touch (the delivery's legality, GameManager.CheckDeliveryLegalityForAI)
    /// within reach, <paramref name="preferred"/> first, else the one with the fewest hit points (a hostile spell's foes, a
    /// helpful spell's hurt allies); with none in reach and a move action left, the first such creature (preferred, then
    /// nearest) a move reaches (GameManager.FindTouchApproachSquareForAI), after moving beside it. Then the delivery
    /// request (GameManager.TryNPCDeliverHeldChargeForAI), the PC's Discharge path with the AI decider. A miss keeps the charge.
    /// </summary>
    private IEnumerator TryDeliverHeldChargeTurn(CharacterController npc, CharacterController preferred)
    {
        SpellcastingComponent sc = npc != null ? npc.Spellcasting : null;
        if (_gameManager == null || sc == null || !sc.HasHeldTouchCharge || sc.HeldTouchSpell == null || CombatEnded())
            yield break;
        SpellData held = sc.HeldTouchSpell;
        bool hostile = !AISpellcastingStrategist.IsAllyTargetedSpell(held);
        CharacterController target = null;
        int bestHp = int.MaxValue;
        var reachable = new List<CharacterController>();
        foreach (CharacterController c in _gameManager.GetAllCharactersForAI())
        {
            if (c == null || c.Stats == null || c.Stats.IsDead)
                continue;
            bool sideOk = hostile ? TeamUtility.IsEnemy(npc, c) : (c == npc || TeamUtility.IsAlly(npc, c)) && c.Stats.CurrentHP < c.Stats.TotalMaxHP;
            if (!sideOk)
                continue;
            // Only a creature the spell is worth landing on (an inflict spell not on an undead foe, a cure not on an undead
            // ally; AISpellcastingStrategist.IsWorthwhileSpellTarget, the single-target choice's tests).
            if (!AISpellcastingStrategist.IsWorthwhileSpellTarget(npc, held, c, _gameManager))
                continue;
            CastOutcome legality = _gameManager.CheckDeliveryLegalityForAI(npc, c);
            if (legality.Kind != CastOutcomeKind.Refused)
            {
                if (c == preferred)
                {
                    target = c;
                    bestHp = int.MinValue;
                }
                else if (c.Stats.CurrentHP < bestHp)
                {
                    target = c;
                    bestHp = c.Stats.CurrentHP;
                }
            }
            else if (legality.Reason == "out-of-range")
            {
                reachable.Add(c);
            }
        }

        if (target == null && npc.Actions != null && npc.Actions.HasMoveAction && reachable.Count > 0)
        {
            reachable.Sort((a, b) =>
            {
                if (a == preferred) return -1;
                if (b == preferred) return 1;
                return CasterLevelRules.DistanceToTarget(npc, a).CompareTo(CasterLevelRules.DistanceToTarget(npc, b));
            });
            for (int i = 0; i < reachable.Count && target == null; i++)
            {
                Vector2Int? approach = _gameManager.FindTouchApproachSquareForAI(npc, reachable[i]);
                if (!approach.HasValue)
                    continue;
                target = reachable[i];
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("✋", $"{npc.Stats.CharacterName} moves to touch {target.Stats.CharacterName} with the held {held.Name}."));
                var touchMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, approach.Value, _gameManager.GetPlayerMoveSecondsPerStepForAI(), touchMove));
                if (CombatEnded() || ThreatSystem.IsMoverIncapacitated(npc))
                    yield break; // dropped by an attack of opportunity on the way (CMB-073)
                if (touchMove.Refused)
                    yield break; // refused by the shared movement legality (logged): nothing spent, no touch
                npc.Actions.UseMoveAction();
            }
        }

        if (target == null)
        {
            Debug.Log($"[AI][Spell] {npc.Stats.CharacterName} holds {held.Name} but has no creature to touch this turn.");
            yield break;
        }
        if (!sc.HasHeldTouchCharge || target.Stats == null || target.Stats.IsDead
            || _gameManager.CheckDeliveryLegalityForAI(npc, target).Kind == CastOutcomeKind.Refused)
            yield break;

        Debug.Log($"[AI][Spell] {npc.Stats.CharacterName} delivers the held {held.Name} to {target.Stats.CharacterName}");
        _gameManager.TryNPCDeliverHeldChargeForAI(npc, target);
        yield return new WaitForSeconds(0.5f);
    }

    private CharacterController SelectBestSpellTarget(CharacterController caster, SpellData spell, CharacterController fallbackTarget)
    {
        if (_gameManager == null || caster == null || spell == null)
            return fallbackTarget;

        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();
        if (allCombatants == null || allCombatants.Count == 0)
            return fallbackTarget;

        // ── T1.1: Use comprehensive target selection (supports allies AND enemies) ──
        return AISpellcastingStrategist.SelectBestSpellTarget(
            caster, spell, fallbackTarget, allCombatants, _gameManager);
    }

    private static bool HasCreatureTag(CharacterController character, string tag)
    {
        if (character?.Stats?.CreatureTags == null || string.IsNullOrWhiteSpace(tag))
            return false;

        for (int i = 0; i < character.Stats.CreatureTags.Count; i++)
        {
            if (string.Equals(character.Stats.CreatureTags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsMagicMissileOnlyCaster(CharacterController caster)
    {
        if (caster == null)
            return false;

        if (caster.Tags != null && caster.Tags.HasTag("AI:MagicMissileOnly"))
            return true;

        return HasCreatureTag(caster, "AI:MagicMissileOnly");
    }

    /// <summary>
    /// A castable spell and its score (<see cref="ScoreCastableSpells"/>), with the aim of an area spell (batch 11 step 25).
    /// </summary>
    private readonly struct AiScoredSpell
    {
        public readonly SpellData Spell;
        public readonly float Score;
        public readonly AreaPlacement Placement;

        /// <summary>A Summon Monster or Summon Nature's Ally the AI can place (batch 11 step 27).</summary>
        public readonly bool Summon;

        public AiScoredSpell(SpellData spell, float score, AreaPlacement placement = null, bool summon = false)
        {
            Spell = spell;
            Score = score;
            Placement = placement;
            Summon = summon;
        }
    }

    /// <summary>
    /// The aim the AI would cast area spell <paramref name="spell"/> at (batch 11 step 25, AI-001 area half), or null, and
    /// then the spell is no candidate (no fallback): a choice the AI decider cannot make (a wall's ends, a swarm's square;
    /// ICastDecider.CanFill), no worthwhile placement (AreaAimSearch, through <paramref name="profile"/>'s ally sparing
    /// when it has one, SpellcasterAIProfile.FindAreaPlacement), or a placement the cast core's Legality refuses
    /// (GameManager.CheckAreaLegalityForAI: the origin's range and line of effect, the NPC caster tests, the metamagic,
    /// the casting action, Silence and the components). The search runs on the clone the cast will use (its metamagic
    /// applied), which comes back in <paramref name="castSpell"/> so the spell is scored on the same footprint (batch 11
    /// step 25 review). The skip lines are logged only when <paramref name="log"/> (the cast's own walk, not every planning
    /// call of SelectSpell).
    /// </summary>
    private AreaPlacement FindAreaPlacementForAI(CharacterController caster, SpellData spell, SpellcasterAIProfile profile,
        List<CharacterController> allCombatants, bool log, out SpellData castSpell)
    {
        castSpell = null;
        if (_gameManager == null || caster == null || spell == null)
            return null;
        SpellCastRequest probe = _gameManager.BuildAiAreaRequest(caster, spell, null, null);
        castSpell = probe.Spell;
        if (probe.Decider != null && !probe.Decider.CanFill(probe))
        {
            if (log)
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName}: skips {spell.Name}, whose choices the AI cannot make yet (choices-incomplete).");
            return null;
        }
        AreaPlacement placement = profile != null
            ? profile.FindAreaPlacement(probe.Spell, caster, allCombatants, _gameManager)
            : AreaAimSearch.FindBest(probe.Spell, caster, allCombatants, _gameManager.Grid);
        if (placement == null)
        {
            if (log)
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName}: no worthwhile aim for {spell.Name}; it is not a candidate.");
            return null;
        }
        CastOutcome legality = _gameManager.CheckAreaLegalityForAI(caster, spell, placement);
        if (legality.Kind == CastOutcomeKind.Refused)
        {
            if (log)
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName}: the aim of {spell.Name} at {placement} is refused ({legality.Reason}).");
            return null;
        }
        return placement;
    }

    /// <summary>
    /// The highest-scoring castable spell, or null: the first of <see cref="ScoreCastableSpells"/>. The AI's planning reads
    /// it (the spell it would cast, also to size an approach before the caster is in range), so it tests no target or
    /// range; the cast itself picks among legal (spell, target) pairs (<see cref="TryExecuteSpellcastAction"/>).
    /// <paramref name="allow"/> filters the candidates and a candidate must score above <paramref name="minScore"/>
    /// (both used by the dragon turn's cast-first step, AI-037); the defaults keep every caller's old choice.
    /// </summary>
    public SpellData SelectSpell(CharacterController caster, CharacterController target,
        Func<SpellData, bool> allow = null, float minScore = float.NegativeInfinity)
    {
        List<AiScoredSpell> scored = ScoreCastableSpells(caster, target, allow, minScore);
        return scored.Count > 0 ? scored[0].Spell : null;
    }

    /// <summary>
    /// The castable spells the AI would consider, each with its score (the profile's ScoreSpell, else
    /// AISpellcastingStrategist.ScoreSpellComprehensive), best first; equal scores keep the spell list's order, so the
    /// first is the spell the old single pick chose. Left out: a spell <paramref name="allow"/> refuses, one scoring
    /// <paramref name="minScore"/> or less, one with no modelled effect (SPL-135) or a choice the AI cannot give
    /// (Imbue with Spell Ability, AI-071), Dimension Door out of danger (batch 11 step 27), a summon the AI cannot place
    /// (GameManager.CheckSummonLegalityForAI; Summon Swarm, AI-071), a buff already running, one the cast core's caster tests refuse
    /// (GameManager.CheckCasterLegalityForAI: the metamagic, the casting action, Silence, the components; batch 11
    /// step 22, the same CheckLegality the cast runs), and an area spell with no legal, worthwhile aim
    /// (<see cref="FindAreaPlacementForAI"/>, batch 11 step 25), whose aim is kept with its score. An area spell is scored
    /// on the clone its cast uses (a prepared metamagic slot's metamagic applied), the one its aim was searched on.
    /// Since batch 11 step 28 (AI-001) the candidates also include a spontaneous caster's metamagic choices
    /// (AiMetamagicPlanner.SpontaneousCandidates: each known spell with each metamagic feat the rules let it apply now),
    /// and every candidate's metamagic, chosen or prepared, adds AiMetamagicPlanner.Value to its score (what Empower,
    /// Maximize, Extend, Heighten or Quicken gains, less a spontaneous choice's higher spell per day and full-round action).
    /// <paramref name="logAreaSkips"/>: log why an area spell is no candidate (the cast's walk only).
    /// </summary>
    private List<AiScoredSpell> ScoreCastableSpells(CharacterController caster, CharacterController target,
        Func<SpellData, bool> allow, float minScore, bool logAreaSkips = false)
    {
        var result = new List<AiScoredSpell>();
        if (caster == null || caster.Stats == null)
            return result;

        SpellcastingComponent spellcasting = caster.Spellcasting;
        if (spellcasting == null || !spellcasting.CanCastSpells)
            return result;

        // No plain spell left can still leave a spontaneous metamagic one (a 1st-level spell known, only 3rd-level spells
        // per day left: an empowered cast spends one, PHB p.88), so an empty list does not end the choice here.
        List<SpellData> castable = spellcasting.GetCastablePreparedSpells() ?? new List<SpellData>();

        if (IsMagicMissileOnlyCaster(caster))
        {
            for (int i = 0; i < castable.Count; i++)
            {
                SpellData spell = castable[i];
                if (spell != null && string.Equals(spell.SpellId, SpellNames.MAGIC_MISSILE, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new AiScoredSpell(spell, 0f));
                    break;
                }
            }

            return result;
        }

        AIProfile profile = GetProfile(caster);
        SpellcasterAIProfile spellcasterProfile = profile as SpellcasterAIProfile;

        List<CharacterController> allCombatants = _gameManager != null
            ? _gameManager.GetAllCharactersForAI()
            : new List<CharacterController>();

        // Cache StatusEffectManager for buff-already-active checks (prevents wasting spell slots).
        StatusEffectManager statusMgr = caster.StatusEffectManager;

        // The metamagic dimension (batch 11 step 28, AI-001): a spontaneous caster's known spells with each metamagic feat
        // it may apply now (AiMetamagicPlanner.SpontaneousCandidates, each a clone carrying its choice), after the plain
        // and prepared candidates, so an equal score keeps the plain spell first. Each passes the same tests below.
        var candidates = new List<SpellData>(castable);
        candidates.AddRange(AiMetamagicPlanner.SpontaneousCandidates(caster, allCombatants));
        // A cleric's spontaneous conversion (PHB p.32; CHR-070, batch 12 step 24): a cure or inflict spell it has not
        // prepared, cast by giving up a prepared spell (AiConversionPlanner), after the prepared and metamagic candidates.
        candidates.AddRange(AiConversionPlanner.Candidates(caster, castable));
        if (candidates.Count == 0)
            return result;

        for (int i = 0; i < candidates.Count; i++)
        {
            SpellData spell = candidates[i];
            if (spell == null)
                continue;
            if (allow != null && !allow(spell))
                continue; // AI-037

            // A cast with no modelled effect wastes the action (Alter Self, SPL-135).
            if (AISpellcastingStrategist.HasNoModelledCombatEffect(spell))
                continue;

            // A handler that needs a choice the AI cannot give yet (Imbue's slot picker; AI-071).
            if (AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive(spell))
                continue;

            // Dimension Door (PHB p.221; batch 11 step 27, SPL-091): an escape the AI casts only in danger
            // (AISpellcastingStrategist.GetEscapeScore) and with a free escape square (GameManager.ChooseDimensionDoorDestination,
            // the square the AI decider then fills in).
            if (spell.SpellId == SpellNames.DIMENSION_DOOR
                && (_gameManager == null || AISpellcastingStrategist.GetEscapeScore(caster, spell, allCombatants) <= 0f
                    || !_gameManager.ChooseDimensionDoorDestination(caster, spell).HasValue))
                continue;

            // The cast core's caster tests (batch 11 step 22): a prepared metamagic spell's metamagic the rules refuse
            // (MetamagicSystem, batch 11 step 21), a casting action the caster cannot take now (a 1-round casting time
            // without a full-round action left, a second quickened spell this round; CastActionRules, batch 11 step 12,
            // PHB p.174, p.98, p.229), a verbal spell inside a Silence (PHB p.279; batch 11 step 13, SPL-092) and a
            // missing component (PHB p.174; batch 11 step 17), with the metamagic the NPC cast carries.
            if (_gameManager != null)
            {
                if (_gameManager.CheckCasterLegalityForAI(caster, spell) != null)
                    continue;
            }
            else if (CastActionRules.Refusal(caster, spell, SpellCastRequest.AiMetamagicFor(spell), SpellCastRequest.AiSourceFor(caster, spell)) != null
                     || CastComponentRules.SilenceBlocks(caster, spell, SpellCastRequest.AiMetamagicFor(spell), SpellCastRequest.AiSourceFor(caster, spell)))
                continue;

            // A summon (batch 11 step 27, SPL-091): a candidate only when the cast core would let the summon request
            // through and the AI decider finds a creature to call and a square for it (GameManager.CheckSummonLegalityForAI).
            // A Summon Swarm's square is a choice the AI cannot make yet (AiCastDecider.CanFill). Without a GameManager (a
            // static test) summons are no candidates.
            bool isSummon = false;
            if (SummoningService.IsSummonMonsterSpell(spell) || SummoningService.IsSummonSwarmSpell(spell))
            {
                if (_gameManager == null || SummoningService.IsSummonSwarmSpell(spell))
                    continue;
                CastOutcome summonLegality = _gameManager.CheckSummonLegalityForAI(caster, spell);
                if (summonLegality.Kind == CastOutcomeKind.Refused)
                {
                    if (logAreaSkips)
                        Debug.Log($"[AI][Spell] {caster.Stats.CharacterName}: skips {spell.Name}, which it cannot summon now ({summonLegality.Reason}).");
                    continue;
                }
                isSummon = true;
            }

            // An area spell (batch 11 step 25, AI-001 area half): a candidate only with a legal, worthwhile aim, which it
            // keeps for the cast. Without a GameManager (a static test) area spells are no candidates.
            AreaPlacement placement = null;
            SpellData scoredSpell = spell;
            if (AreaAimSearch.IsAreaCastForAI(spell))
            {
                placement = FindAreaPlacementForAI(caster, spell, spellcasterProfile, allCombatants, logAreaSkips, out SpellData castSpell);
                if (placement == null)
                    continue;
                if (castSpell != null)
                    scoredSpell = castSpell;
            }

            // ── Buff-already-active check ──
            // D&D 3.5e: Same spell doesn't stack — skip buff/illusion spells already active on the caster.
            // This prevents AI from wasting actions and spell slots recasting Mage Armor, Shield,
            // Mirror Image, etc. when they're already providing their benefit.
            if ((spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Illusion) && statusMgr != null)
            {
                bool alreadyActive = statusMgr.HasEffect(spell.SpellId);
                if (alreadyActive)
                {
                    int remaining = statusMgr.GetRemainingRounds(spell.SpellId);
                    // Allow recasting if about to expire (≤ 1 round remaining) — useful for
                    // long fights where the dragon wants to maintain its buffs.
                    // -1 = indefinite; -2 = a spell it concentrates on, which a recast would only end and restart (PHB p.176)
                    if (remaining > 1 || remaining == -1 || remaining == SpellDurationRules.ConcentrationRounds)
                    {
                        Debug.Log($"[AI][Spell] {caster.Stats.CharacterName}: Skipping {spell.Name} — already active ({remaining} rounds remaining)");
                        continue;
                    }
                }
            }

            float score;

            if (spellcasterProfile != null)
            {
                score = spellcasterProfile.ScoreSpell(scoredSpell, caster, target, allCombatants, _gameManager);
            }
            else
            {
                // ── Comprehensive scoring for non-profile casters (Tiers 1–4) ──
                score = AISpellcastingStrategist.ScoreSpellComprehensive(
                    scoredSpell, caster, target, allCombatants, _gameManager);
            }

            // The metamagic the candidate carries, a prepared slot's or a spontaneous choice (batch 11 step 28, AI-001):
            // what it adds less what a spontaneous choice costs (AiMetamagicPlanner.Value, on the candidate as listed).
            float metamagicValue = AiMetamagicPlanner.Value(caster, spell, target, allCombatants, placement);
            if (metamagicValue != 0f)
            {
                score += metamagicValue;
                if (logAreaSkips)
                    Debug.Log($"[AI][Spell] {caster.Stats.CharacterName}: {AiMetamagicPlanner.Describe(spell)} scores {score:F1} (metamagic {metamagicValue:+0.0;-0.0}).");
            }

            if (score > minScore)
                result.Add(new AiScoredSpell(spell, score, placement, isSummon));
        }

        // Best first; a stable sort keeps the list order among equal scores (the old pick took the first best).
        var ordered = new List<AiScoredSpell>(result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            int at = ordered.Count;
            while (at > 0 && ordered[at - 1].Score < result[i].Score)
                at--;
            ordered.Insert(at, result[i]);
        }
        return ordered;
    }

    public AIActionType SelectBestAction(CharacterController npc, CharacterController target, bool preferAggression)
    {
        if (npc == null || target == null)
            return AIActionType.Wait;

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        bool canCharge = _gameManager.ShouldNPCUseChargeForAI(npc, target);

        AIProfile profile = GetProfile(npc);
        bool profilePrefersCharge = profile == null || profile.ShouldPreferCharge(npc, target, distance, preferAggression);

        Debug.Log($"[AI SelectBestAction] {npc.Stats.CharacterName} → {target.Stats.CharacterName}: dist={distance}, canCharge={canCharge}, profilePrefersCharge={profilePrefersCharge}, hpPct={hpPercent:F2}");

        if (!preferAggression && hpPercent < 0.30f && npc.Actions.HasMoveAction)
            return AIActionType.Retreat;

        if (canCharge && profilePrefersCharge)
            return AIActionType.Charge;

        if (npc.IsTargetInCurrentWeaponRange(target))
        {
            if (ShouldUseManeuver(npc, target))
                return AIActionType.SpecialManeuver;
            return AIActionType.Attack;
        }

        if (distance > 1 && npc.Actions.HasMoveAction)
            return AIActionType.Move;

        return AIActionType.Wait;
    }

    private CharacterController SelectLowestHPEnemy(CharacterController npc)
    {
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        CharacterController weakest = null;
        int lowestHP = int.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(npc, candidate))
                continue;

            if (candidate.Stats.CurrentHP < lowestHP)
            {
                lowestHP = candidate.Stats.CurrentHP;
                weakest = candidate;
            }
        }

        return weakest;
    }

    private int GetMaximumAttackRangeInSquares(CharacterController npc)
    {
        ItemData weapon = npc.GetEquippedMainWeapon();
        if (weapon == null)
            return 1;

        if (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0)
            return RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, weapon.IsThrown);

        return 1;
    }

    // ==================== AI COUNTERSPELL SUPPORT ====================

    /// <summary>
    /// Evaluate whether an NPC spellcaster should ready a counterspell this turn.
    /// AI readies counterspell when:
    /// - The NPC is a spellcaster with available spell slots
    /// - There are enemy spellcasters that pose a threat
    /// - The NPC has Spellcraft ranks (for spell identification)
    /// - The NPC has Dispel Magic or likely-to-be-cast enemy spells prepared
    /// </summary>
    /// <param name="npc">The NPC to evaluate.</param>
    /// <returns>True if the AI decided to ready a counterspell.</returns>
    public bool TryAIReadyCounterspell(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || npc.Stats.IsDead) return false;
        if (!npc.Actions.HasStandardAction) return false;

        // Must be a spellcaster
        var spellComp = npc.Spellcasting;
        if (spellComp == null || !spellComp.CanCastSpells) return false;

        // Need at least some Spellcraft or Dispel Magic to be useful
        int spellcraftBonus = npc.Stats.GetSkillBonus("Spellcraft");
        bool hasDispelMagic = npc.HasDispelMagicAvailable();
        if (spellcraftBonus <= 0 && !hasDispelMagic) return false;

        // Find enemy spellcasters that are alive and nearby
        CharacterController bestTarget = null;
        int bestThreat = 0;

        foreach (var c in _gameManager.GetAllCharactersForAI())
        {
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (!TeamUtility.IsEnemy(npc, c)) continue; // allies and neutrals (AI-016)

            // Check if they're a spellcaster
            if (!c.Stats.IsSpellcaster) continue;

            var enemySpellComp = c.Spellcasting;
            if (enemySpellComp == null || !enemySpellComp.HasAnyCastablePreparedSpell()) continue;

            int distance = SquareGridUtils.GetDistance(npc.GridPosition, c.GridPosition);
            int casterLevel = Mathf.Max(1, npc.Stats.GetCasterLevel());
            int dispelRange = (100 + 10 * casterLevel) / 5;

            if (distance > dispelRange) continue;

            // Threat based on caster level and proximity
            int threat = c.Stats.GetCasterLevel() * 10 - distance;
            if (threat > bestThreat)
            {
                bestThreat = threat;
                bestTarget = c;
            }
        }

        if (bestTarget == null) return false;

        // AI decision: ready counterspell if there's a significant threat
        // Only do this ~30% of the time to keep AI varied
        if (UnityEngine.Random.Range(0, 100) > 30) return false;

        int currentRound = _gameManager.CurrentRoundNumber;
        bool readied = npc.ReadyCounterspell(bestTarget, currentRound);

        if (readied)
        {
            // AI prefers same-spell counter over Dispel Magic (auto-success vs check)
            // But will use Dispel Magic as fallback
            if (npc.ReadiedCounterspell != null)
                npc.ReadiedCounterspell.PreferDispelMagic = !hasDispelMagic ? false : (spellcraftBonus < 5);

            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Special("⚡", $"{npc.Stats.CharacterName} readies a counterspell against {bestTarget.Stats.CharacterName}!"));
        }

        return readied;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AI WALL OF ICE INTERACTION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// If the NPC is adjacent to an intact Wall of Ice, decides whether to attack it
    /// with a weapon (auto-hit, Hardness 0) or attempt a Strength check (DC 15 + CL).
    /// Prefers weapon attack if equipped; falls back to STR check for unarmed NPCs.
    /// If not adjacent to any wall, yields a short delay and returns.
    /// </summary>
    private IEnumerator TryAIWallInteraction(CharacterController npc, CharacterController target)
    {
        if (npc == null || !npc.CanTakeTurnActions() || !npc.Actions.HasStandardAction)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        Vector2Int targetPos = target != null ? target.GridPosition : npc.GridPosition;
        Vector2Int? bestWallCell = _gameManager.FindBestAdjacentWallCellForAI(npc, targetPos);

        if (!bestWallCell.HasValue)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(bestWallCell.Value);
        if (wall == null || wall.IsBreached(bestWallCell.Value))
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        // Decide: weapon attack vs Strength check
        // Weapon attacks auto-hit and deal full damage (Hardness 0), so prefer weapon.
        // STR check is a backup if STR bonus is high enough for reasonable success.
        bool hasWeapon = npc.GetEquippedMainWeapon() != null || npc.Stats.GetPrimaryNaturalAttack() != null;
        int strMod = CharacterStats.GetModifier(npc.Stats.STR);
        int dc = wall.GetStrengthCheckDC();
        bool strCheckLikely = (strMod + 10) >= dc; // Rough: ~50%+ chance on d20

        if (hasWeapon)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} attacks Wall of Ice at ({bestWallCell.Value.x},{bestWallCell.Value.y}) with weapon.");
            yield return _gameManager.StartCoroutine(_gameManager.NPCAttackWallForAI(npc, wall, bestWallCell.Value));
        }
        else if (strCheckLikely)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} attempts STR check (DC {dc}) on Wall of Ice at ({bestWallCell.Value.x},{bestWallCell.Value.y}).");
            yield return _gameManager.StartCoroutine(_gameManager.NPCBreakWallForAI(npc, wall, bestWallCell.Value));
        }
        else
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} is adjacent to Wall of Ice but STR check DC {dc} too high (STR mod {strMod}), skipping.");
            yield return new WaitForSeconds(0.3f);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PHASE 6A: EXTRACTED AI DECISION METHODS
    //  Centralized from GameManager to reduce coupling and improve reuse.
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Finds the closest alive enemy to the given character using Chebyshev distance.
    /// Pure decision method — no side effects.
    /// </summary>
    /// <param name="source">The character to measure from.</param>
    /// <param name="allCharacters">All characters currently in combat.</param>
    /// <returns>The closest alive enemy, or null if none found.</returns>
    public static CharacterController FindClosestAliveEnemy(CharacterController source, List<CharacterController> allCharacters)
    {
        if (source == null || allCharacters == null) return null;

        CharacterController closest = null;
        int closestDist = int.MaxValue;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController candidate = allCharacters[i];
            if (candidate == null || candidate == source || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(source, candidate))
                continue;

            int dist = SquareGridUtils.GetDistance(source.GridPosition, candidate.GridPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = candidate;
            }
        }

        return closest;
    }

    /// <summary>
    /// Finds the closest alive enemy within a specified range (in feet).
    /// Used for free-action ranged special attacks (e.g., Spittle) that need a target in range.
    /// </summary>
    private CharacterController FindClosestEnemyInRange(CharacterController source, int rangeFeet)
    {
        if (source == null || _gameManager == null) return null;

        List<CharacterController> allCharacters = _gameManager.GetAllCharactersForAI();
        if (allCharacters == null) return null;

        int rangeSquares = rangeFeet / 5;
        CharacterController closest = null;
        int closestDist = int.MaxValue;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController candidate = allCharacters[i];
            if (candidate == null || candidate == source || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(source, candidate))
                continue;
            // A creature holding with one body part attacks only other opponents (MM p.310; delegated RAW reading
            // 2026-10-14, CMB-163), so a free Spittle goes to another foe in range; nor across a Resilient Sphere or at a
            // foe whose ward it failed to overcome (AttackLegality, read only; CMB-092, D6).
            if (!AttackLegality.Check(source, candidate, DirectAttackKind.Touch, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward).Ok)
                continue;

            int dist = SquareGridUtils.GetDistance(source.GridPosition, candidate.GridPosition);
            if (dist <= rangeSquares && dist < closestDist)
            {
                closestDist = dist;
                closest = candidate;
            }
        }

        return closest;
    }

    /// <summary>
    /// Selects the best target for a summoned creature based on its current command.
    /// Supports AttackNearest (targets closest to summon) and ProtectCaster (targets closest to caster).
    /// </summary>
    /// <param name="summon">The summoned creature selecting a target.</param>
    /// <param name="caster">The caster who summoned the creature (used for ProtectCaster command).</param>
    /// <param name="commandType">The current summon command type.</param>
    /// <param name="allCharacters">All characters currently in combat.</param>
    /// <returns>The best target for the summon, or null if no enemies found.</returns>
    public static CharacterController SelectSummonTarget(
        CharacterController summon,
        CharacterController caster,
        SummonCommandType commandType,
        List<CharacterController> allCharacters)
    {
        if (summon == null || allCharacters == null) return null;

        List<CharacterController> enemies = new List<CharacterController>();
        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController candidate = allCharacters[i];
            if (candidate == null || candidate == summon || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(summon, candidate))
                continue;
            // A summon holding with one body part attacks only other opponents (MM p.310; delegated RAW reading
            // 2026-10-14, CMB-163); nor across a Resilient Sphere or at a foe whose ward it failed to overcome
            // (AttackLegality, read only; CMB-092, D6).
            if (!AttackLegality.Check(summon, candidate, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward).Ok)
                continue;
            enemies.Add(candidate);
        }

        if (enemies.Count == 0) return null;

        switch (commandType)
        {
            case SummonCommandType.ProtectCaster:
                return FindNearestEnemy(enemies, caster ?? summon);
            case SummonCommandType.AttackNearest:
            default:
                return FindNearestEnemy(enemies, summon);
        }
    }

    /// <summary>
    /// Finds the nearest enemy to a reference character by Chebyshev distance.
    /// Pure utility — used by summon targeting, retreat logic, and turn-undead flee.
    /// </summary>
    /// <param name="enemies">The pool of candidate enemies.</param>
    /// <param name="referenceCharacter">The character to measure distance from.</param>
    /// <returns>The nearest enemy, or null if the list is empty.</returns>
    public static CharacterController FindNearestEnemy(List<CharacterController> enemies, CharacterController referenceCharacter)
    {
        if (enemies == null || enemies.Count == 0 || referenceCharacter == null) return null;

        CharacterController nearest = null;
        int nearestDist = int.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController enemy = enemies[i];
            if (enemy == null) continue;
            int dist = SquareGridUtils.GetDistance(referenceCharacter.GridPosition, enemy.GridPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    /// <summary>
    /// True when the NPC's profile asks for bull rushes and a bull rush charge at this target is
    /// legal (PHB p.154-155, CMB-102). Asked only after ShouldNPCCharge has chosen a weapon charge, so
    /// an NPC with no melee weapon, or whose only legal charge is a bull rush, never considers one
    /// (AI-014). No shipped profile sets AttemptBullRush yet.
    /// </summary>
    public bool ShouldNPCChargeBullRush(CharacterController npc, CharacterController target)
    {
        AIProfile profile = GetProfile(npc);
        return profile?.Maneuvers?.AttemptBullRush == true
            && _gameManager != null
            && _gameManager.CanChargeTargetForAI(npc, target, true);
    }

    /// <summary>
    /// The push decision after a successful bull rush by a non-controllable attacker (PHB p.154,
    /// CMB-098): stay and push 5 ft (returns 1, follow false), or move with the defender and push
    /// 1 to <paramref name="maxIfFollowing"/> squares. The attacker's AI profile decides
    /// (AIProfile.ChooseBullRushPush); without a profile the attacker follows for the maximum.
    /// Called by GameManager.ResolveBullRushPushAndFollow, the resolver the PC prompt also feeds.
    /// </summary>
    public int ChooseBullRushPush(CharacterController attacker, CharacterController target, int maxIfFollowing, out bool follow)
    {
        int max = Mathf.Max(1, maxIfFollowing);
        AIProfile profile = GetProfile(attacker);
        int squares;
        if (profile != null)
        {
            squares = profile.ChooseBullRushPush(attacker, target, max, out follow);
        }
        else
        {
            follow = true;
            squares = max;
        }

        return follow ? Mathf.Clamp(squares, 1, max) : 1;
    }

    /// <summary>
    /// Determines whether an NPC should use a charge action to reach a target.
    /// D&D 3.5e PHB p.154: Charge is a full-round action when target is beyond melee reach
    /// but reachable via a valid straight-line charge path; a creature limited to one action makes a partial charge
    /// (a standard action, up to its speed; D2, CMB-108), which the charge core's legality admits.
    /// </summary>
    /// <param name="npc">The NPC considering a charge.</param>
    /// <param name="target">The intended charge target.</param>
    /// <returns>True if the NPC should charge; false if melee is already possible or charge is invalid.</returns>
    public bool ShouldNPCCharge(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null) return false;
        // A full-round action, or a partial charge for a creature limited to one action (PHB p.154, D2; CMB-108).
        if (!ChargeRules.HasChargeAction(npc, out bool partialCharge, out string chargeActionReason))
        {
            Debug.Log($"[AI Charge] {npc?.Stats?.CharacterName}: {chargeActionReason} Skipping charge.");
            return false;
        }
        if (!npc.HasMeleeWeaponEquipped())
        {
            Debug.Log($"[AI Charge] {npc?.Stats?.CharacterName}: no melee weapon equipped, skipping charge.");
            return false;
        }
        if (target.Stats == null || target.Stats.IsDead) return false;

        int dist = npc.GetMinimumDistanceToTarget(target, chebyshev: true);
        // Already in melee range — charge not needed
        if (npc.CanMeleeAttackDistance(dist))
        {
            Debug.Log($"[AI Charge] {npc.Stats.CharacterName}: already in melee range (dist={dist}), no charge needed.");
            return false;
        }

        bool canCharge = _gameManager.CanChargeTargetForAI(npc, target);
        Debug.Log($"[AI Charge] {npc.Stats.CharacterName} → {target.Stats.CharacterName}: dist={dist}, canCharge={canCharge}, partial={partialCharge}");
        return canCharge;
    }

    /// <summary>
    /// Selects the best target during an adaptive full-attack sequence.
    /// When the current target dies or becomes invalid mid-attack, this method finds the next-best
    /// in-range target using profile scoring and AIService target selection.
    /// </summary>
    /// <param name="attacker">The attacking NPC.</param>
    /// <param name="profile">The NPC's AI profile for target scoring.</param>
    /// <param name="allCharacters">All characters in combat.</param>
    /// <param name="requireInRange">If true, only considers targets within current weapon range.</param>
    /// <returns>The best adaptive target, or null if none available.</returns>
    public CharacterController SelectAdaptiveFullAttackTarget(
        CharacterController attacker,
        AIProfile profile,
        List<CharacterController> allCharacters,
        bool requireInRange,
        Func<CharacterController, bool> inRange = null)
    {
        if (attacker == null || attacker.Stats == null)
            return null;

        var enemies = new List<CharacterController>();
        bool hasConsciousEnemy = false;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController candidate = allCharacters[i];
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(attacker, candidate))
                continue;
            // A creature holding with one body part attacks only other opponents (MM p.310, CMB-149); the sphere and a
            // known failed ward save (CMB-092, AI-005), read only.
            if (!AttackLegality.Check(attacker, candidate, DirectAttackKind.Weapon, null,
                    AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward).Ok)
                continue;

            enemies.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = profile != null
            && profile.ShouldIgnoreUnconsciousTargets(attacker)
            && hasConsciousEnemy;

        var candidates = new List<CharacterController>();
        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController candidate = enemies[i];
            if (ignoreUnconscious && candidate.IsUnconscious)
                continue;

            // In reach or range for the step's mode (a throw's thrown range; batch 12 step 17), else the weapon's reach or range.
            if (requireInRange && !(inRange != null ? inRange(candidate) : AiAttackGuard.CheckTarget(attacker, candidate, null).Ok))
                continue;

            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return null;

        // Use full AIService target selection first
        CharacterController profiled = SelectBestTarget(attacker, candidates);
        if (profiled != null)
            return profiled;

        // Fallback: profile-based scoring
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController candidate = candidates[i];
            float score = profile != null ? profile.ScoreTarget(candidate, attacker) : 0f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Determines if a creature is a Giant Bombardier Beetle by NPC definition ID.
    /// Used to trigger special acid spray attacks during AI turns.
    /// </summary>
    /// <param name="npc">The NPC to check.</param>
    /// <returns>True if the NPC is a Giant Bombardier Beetle.</returns>
    public static bool IsGiantBombardierBeetle(CharacterController npc)
    {
        if (npc?.Stats == null) return false;
        return string.Equals(npc.Stats.SourceNpcDefinitionId, "giant_bombardier_beetle", System.StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Special Ability Execution Methods
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempt to execute a ranged special attack (Spittle, Web, Acid Spray).
    /// Returns true if the attack was performed, false otherwise.
    /// </summary>
    public bool TryExecuteRangedSpecialAttack(CharacterController npc, CharacterController target)
    {
        if (_gameManager == null || npc == null || target == null || npc.Stats == null)
            return false;

        if (!npc.IsRangedSpecialAttackReady)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName}'s ranged special attack not ready (cooldown remaining).");
            return false;
        }

        RangedSpecialAttackDefinition attack = npc.GetRangedSpecialAttackDefinition();
        if (attack == null)
            return false;

        // A ranged touch attack is an attack, so a holder may not make it against the creature it holds with one body
        // part (MM p.310; delegated RAW reading 2026-10-14, CMB-163).
        if (npc.IsBarredFromOrdinaryAttackOn(target, out string holdReason))
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} cannot use {attack.Name} on {target.Stats.CharacterName}: {holdReason}");
            return false;
        }

        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        if (distance > attack.RangeFeet / 5)  // Convert feet to squares (5 ft = 1 square)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName}'s target is too far ({distance} squares) for {attack.Name} (range {attack.RangeFeet} ft).");
            return false;
        }

        // Nothing passes a Resilient Sphere (CMB-092), and a known failed ward save bars the target (D6); read only.
        Legality specialLegality = AttackLegality.Check(npc, target, DirectAttackKind.Touch, null,
            AttackLegalityChecks.Sphere | AttackLegalityChecks.Ward);
        if (!specialLegality.Ok)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} cannot use {attack.Name} on {target.Stats.CharacterName}: {specialLegality.Reason}");
            return false;
        }

        // A ranged touch attack is a direct attack: its attempt rolls the attacker's one Sanctuary save (PHB p.274; D6); a
        // failure loses the attack and its action but not the ability's use, which never happened, as a spell keeps its
        // slot (delegated RAW reading 2026-10-28, D6).
        if (AttackLegality.CommitWard(npc, target, DirectAttackKind.Touch) == WardVerdict.Failed)
        {
            if (!attack.IsFreeAction)
                npc.CommitStandardAction();
            return true;
        }

        // A ranged special that is not a free action is a standard action; it pays through CommitStandardAction, so a
        // disabled creature takes its strenuous 1 hp (PHB p.145; batch 12 D5, AI-053).
        if (!attack.IsFreeAction && !npc.CommitStandardAction())
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} has no standard action left for {attack.Name}.");
            return false;
        }

        // Log the attack
        _gameManager.CombatUI?.ShowCombatLog(
            CombatLogHelper.Info("", $"{npc.Stats.CharacterName} uses {attack.Name} against {target.Stats.CharacterName}!"));

        // Apply cooldown
        npc.ApplyRangedSpecialAttackCooldown();

        // Determine hit/miss and apply damage. The ranged touch attack attacks the target (the acid spray's trigger, D1),
        // whose reaction comes once the attack and its on-hit effect have resolved.
        _gameManager.DeferAttackedReactions();
        try
        {
            _gameManager.NotifyAttackedOrDisturbed(target, npc);
            bool isHit = RollRangedTouchAttack(npc, target);

            if (isHit)
            {
                // Energy damage (spittle acid) ignores damage reduction, physical damage takes it as a natural attack
                // (MM p.307); immunity and resistance apply either way; then the shared concentration and death checks
                // (SPL-004, AI-006).
                int damage = RollDamage(attack.DamageDice, attack.DamageCount);
                DamagePacket specialPacket = DamagePackets.CreatureAttack($"{npc.Stats.CharacterName}'s {attack.Name}", attack.DamageType, true);
                specialPacket.SourceCreature = npc;
                DamageResolutionResult dealt = _gameManager.DealDamage(target, damage, specialPacket);
                _gameManager.CombatUI?.ShowCombatLog(
                    CombatLogHelper.Damage("💥", $"{npc.Stats.CharacterName}'s {attack.Name} hits for {dealt.FinalDamage} {attack.DamageType} damage{GameManager.DescribeMitigation(dealt)}!"));

                // Check for on-hit effects (blinding on crit, etc.)
                if (!string.IsNullOrEmpty(attack.OnHitStatusEffectType) && !attack.OnHitOnCritOnly)
                {
                    ApplyStatusEffectFromSpecialAttack(target, attack, npc);
                }
            }
            else
            {
                _gameManager.CombatUI?.ShowCombatLog(
                    CombatLogHelper.Failure("❌", $"{npc.Stats.CharacterName}'s {attack.Name} misses!"));
            }
        }
        finally
        {
            _gameManager.ResumeAttackedReactions();
        }

        return true;
    }

    /// <summary>
    /// Attempt to execute an Engulf attack. Returns true if engulf was attempted.
    /// </summary>
    public bool TryExecuteEngulf(CharacterController npc, CharacterController target)
    {
        if (_gameManager == null || npc == null || target == null || npc.Stats == null)
            return false;

        EngulfDefinition engulf = npc.GetEngulfDefinition();
        if (engulf == null)
            return false;

        // Must be adjacent
        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        if (distance != 1)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} cannot engulf — target is {distance} squares away (must be adjacent).");
            return false;
        }

        // Engulf is a standard action (MM p.202, gelatinous cube), paid through CommitStandardAction (PHB p.145; AI-053).
        if (!npc.CommitStandardAction())
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} has no standard action left to engulf.");
            return false;
        }

        _gameManager.CombatUI?.ShowCombatLog(
            CombatLogHelper.Info("", $"{npc.Stats.CharacterName} attempts to engulf {target.Stats.CharacterName}!"));

        // Target makes Reflex save to avoid
        var saveResult = SavingThrowResolver.ResolveReflexSave(target.Stats, engulf.ReflexSaveDC, "Engulf",
            SaveContext.CreatureAbility("Engulf", npc));
        
        if (saveResult.Succeeded)
        {
            _gameManager.CombatUI?.ShowCombatLog(
                CombatLogHelper.Success("💨", $"{target.Stats.CharacterName} evades engulf (Reflex {saveResult.Total} vs DC {engulf.ReflexSaveDC})!"));
            return true;
        }

        _gameManager.CombatUI?.ShowCombatLog(
            CombatLogHelper.Damage("⚠", $"{target.Stats.CharacterName} is engulfed (Reflex {saveResult.Total} vs DC {engulf.ReflexSaveDC})!"));
        
        // Apply engulfed condition
        target.ApplyEngulfedCondition(npc, engulf);

        return true;
    }

    /// <summary>
    /// Activate terrain manipulation (Ground Manipulation bog, etc.).
    /// This creates difficult terrain around the NPC.
    /// </summary>
    public void ActivateTerrainManipulation(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || !npc.HasTerrainManipulation)
            return;

        TerrainManipulationDefinition terrain = npc.GetTerrainManipulationDefinition();
        if (terrain == null)
            return;

        _gameManager.CombatUI?.ShowCombatLog(
            CombatLogHelper.Info("", $"{npc.Stats.CharacterName} activates {terrain.Name} in a {terrain.RadiusFeet}-ft radius!"));

        // Mark terrain as difficult in the area around the NPC
        // This would typically interact with the GridSystem to mark affected cells
        // For now, we just log it and track the state
        Debug.Log($"[AI] {npc.Stats.CharacterName} activates {terrain.Name} around {npc.GridPosition} (radius {terrain.RadiusFeet} ft)");
    }

    // ══════════════════════════════════════════════════════════════════
    //  Aura Ability Processing (Gibbering, Frightful Presence, etc.)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Process a creature's supernatural aura ability at the start of its turn.
    /// Per D&D 3.5e, abilities like Gibbering (Su) affect all creatures within range
    /// each round as a free action. Targets must make a saving throw or be affected.
    /// Supports: random duration (1d2, 1d4, etc.), 24-hour save immunity, mind-affecting immunity.
    /// </summary>
    private void ProcessAuraAbility(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || !npc.HasAuraAbility)
            return;

        AuraAbilityDefinition aura = npc.GetAuraAbilityDefinition();
        if (aura == null) return;

        string npcName = npc.Stats.CharacterName;
        int rangeSquares = aura.RangeFeet / 5;
        SaveType saveType = aura.IsWillSave ? SaveType.Will : SaveType.Fortitude;
        string saveTypeName = aura.IsWillSave ? "Will" : "Fort";

        Debug.Log($"[AI][Aura] {npcName} activating {aura.Name} (DC {aura.SaveDC}, {saveTypeName}, range {aura.RangeFeet} ft, effect {aura.Effect}, mind-affecting={aura.IsMindAffecting})");

        _gameManager.CombatUI?.ShowCombatLog(
            CombatLogHelper.Special("🔊", $"{npcName}'s {aura.Name} echoes across the battlefield!"));

        List<CharacterController> allChars = _gameManager.GetAllCharactersForAI();
        int affected = 0;

        foreach (CharacterController candidate in allChars)
        {
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            // Only affect enemies
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            // Mind-affecting immunity check (D&D 3.5e: Gibbering is sonic mind-affecting compulsion)
            if (aura.IsMindAffecting && candidate.Stats.Immunities != null
                && candidate.Stats.Immunities.immuneToMindAffecting)
            {
                Debug.Log($"[AI][Aura] {candidate.Stats.CharacterName} is immune to mind-affecting effects — skipping {aura.Name}");
                continue;
            }

            int distance = SquareGridUtils.GetDistance(npc.GridPosition, candidate.GridPosition);
            if (distance > rangeSquares)
            {
                Debug.Log($"[AI][Aura] {candidate.Stats.CharacterName} out of range ({distance} > {rangeSquares} squares)");
                continue;
            }

            // Already affected by this condition? Skip re-application
            CombatConditionType conditionType = AuraEffectToCondition(aura.Effect);
            if (candidate.HasCondition(conditionType))
            {
                Debug.Log($"[AI][Aura] {candidate.Stats.CharacterName} already has {conditionType}, skipping");
                continue;
            }

            // 24-hour save immunity check (per MM: successful save = immune for 24 hours)
            if (aura.GrantsImmunityOnSave && HasAuraSaveImmunity(candidate, npc, aura.Name))
            {
                Debug.Log($"[AI][Aura] {candidate.Stats.CharacterName} has 24-hour immunity to {npc.Stats.CharacterName}'s {aura.Name} — skipping");
                continue;
            }

            // Roll saving throw
            SaveResult saveResult = SpellSaveResolver.RollSave(candidate, saveType, aura.SaveDC, AuraSaveContext(aura, conditionType));

            if (saveResult.Saved)
            {
                Debug.Log($"[AI][Aura] {candidate.Stats.CharacterName} saves vs {aura.Name} ({saveTypeName} {saveResult.Total} >= DC {aura.SaveDC})");
                _gameManager.CombatUI?.ShowCombatLog(
                    CombatLogHelper.Success("💪", $"{candidate.Stats.CharacterName} resists {aura.Name} ({saveTypeName} {saveResult.Total} vs DC {aura.SaveDC})."));
                
                // Grant 24-hour save immunity if applicable
                if (aura.GrantsImmunityOnSave)
                {
                    GrantAuraSaveImmunity(candidate, npc, aura.Name);
                    _gameManager.CombatUI?.ShowCombatLog(
                        CombatLogHelper.Info("🛡", $"{candidate.Stats.CharacterName} is now immune to {npcName}'s {aura.Name} for 24 hours."));
                }
            }
            else
            {
                // Roll random duration if DurationDice is set, otherwise use fixed DurationRounds
                int duration = aura.DurationDice > 0
                    ? Random.Range(1, aura.DurationDice + 1)  // 1d(DurationDice)
                    : aura.DurationRounds;

                Debug.Log($"[AI][Aura] {candidate.Stats.CharacterName} fails vs {aura.Name} ({saveTypeName} {saveResult.Total} < DC {aura.SaveDC}) — applying {aura.Effect} for {duration} round(s)");
                candidate.ApplyCondition(conditionType, duration, npcName);
                _gameManager.CombatUI?.ShowCombatLog(
                    CombatLogHelper.Debuff("🌀", $"{candidate.Stats.CharacterName} succumbs to {aura.Name}! ({saveTypeName} {saveResult.Total} vs DC {aura.SaveDC}) — {aura.Effect} for {duration} round(s)!"));
                affected++;
            }
        }

        if (affected > 0)
            Debug.Log($"[AI][Aura] {npcName}'s {aura.Name} affected {affected} creature(s)");
    }

    // ── Aura Save Immunity Tracking ──
    // Per MM rules (e.g., Gibbering, Frightful Presence): successful save = 24-hour immunity.
    // Key format: "AuraImmunity_{targetInstanceId}_{sourceInstanceId}_{auraName}"
    // We track this as a simple HashSet since combat encounters don't last 24 hours;
    // immunity persists for the remainder of the encounter.

    private static readonly HashSet<string> _auraSaveImmunities = new HashSet<string>();

    private static string GetAuraImmunityKey(CharacterController target, CharacterController source, string auraName)
    {
        return $"AuraImmunity_{target.GetInstanceID()}_{source.GetInstanceID()}_{auraName}";
    }

    private static bool HasAuraSaveImmunity(CharacterController target, CharacterController source, string auraName)
    {
        return _auraSaveImmunities.Contains(GetAuraImmunityKey(target, source, auraName));
    }

    private static void GrantAuraSaveImmunity(CharacterController target, CharacterController source, string auraName)
    {
        _auraSaveImmunities.Add(GetAuraImmunityKey(target, source, auraName));
        Debug.Log($"[AI][Aura] Granted 24-hr immunity: {target.Stats.CharacterName} vs {source.Stats.CharacterName}'s {auraName}");
    }

    /// <summary>Call at encounter end or scene load to clear aura immunities.</summary>
    public static void ClearAuraSaveImmunities()
    {
        _auraSaveImmunities.Clear();
    }

    /// <summary>
    /// Drops every aura save immunity in which <paramref name="character"/> is the aura's source or the target
    /// (MM: a creature that saves is immune to that one creature's aura for 24 hours). Used when the controller is
    /// reused for a different creature, which is not the creature that was saved against (CRE-046).
    /// </summary>
    public static void ForgetAuraSaveImmunities(CharacterController character)
    {
        if (character == null || _auraSaveImmunities.Count == 0)
            return;

#pragma warning disable CS0618 // the immunity keys are built from GetInstanceID (GetAuraImmunityKey)
        string id = character.GetInstanceID().ToString();
#pragma warning restore CS0618
        _auraSaveImmunities.RemoveWhere(key =>
        {
            // Key: AuraImmunity_<targetId>_<sourceId>_<auraName>
            string[] parts = key.Split('_');
            return parts.Length >= 3 && (parts[1] == id || parts[2] == id);
        });
    }

    /// <summary>
    /// Map an AuraEffectType to the corresponding CombatConditionType.
    /// </summary>
    private static CombatConditionType AuraEffectToCondition(AuraEffectType effect)
    {
        return effect switch
        {
            AuraEffectType.Confused    => CombatConditionType.Confused,
            AuraEffectType.Frightened  => CombatConditionType.Frightened,
            AuraEffectType.Sickened    => CombatConditionType.Sickened,
            AuraEffectType.Fascinated  => CombatConditionType.Fascinated,
            AuraEffectType.Fear        => CombatConditionType.Frightened,
            AuraEffectType.Petrified   => CombatConditionType.Petrified,
            _                          => CombatConditionType.Confused,
        };
    }

    /// <summary>The d20 context of a monster's ranged special attack roll (the scenario harness forces it by this label).</summary>
    public const string SpecialRangedTouchContext = "Special attack ranged touch";

    /// <summary>
    /// A monster's ranged touch attack (spittle, MM p.126) with the terms of a spell's ranged touch attack (AI-006):
    /// SpellCaster.SpellTouchAttack's bonus (BAB + Dex + size, the racial bonus against the target's kind, fighting
    /// defensively, shooting into melee, PHB p.140), SpellCaster.TouchAttackAC (touch AC, fighting defensively, the
    /// dwarf's and gnome's dodge bonus, a protection ward), a natural 20 always hits and a natural 1 always misses
    /// (PHB p.134), then the target's concealment miss chance (PHB p.152). Returns true if the attack hits.
    /// </summary>
    private bool RollRangedTouchAttack(CharacterController attacker, CharacterController target)
    {
        if (attacker?.Stats == null || target?.Stats == null)
            return false;

        SpellCaster.SpellTouchAttackTerms terms = SpellCaster.SpellTouchAttack(null, attacker, attacker.Stats, target, target.Stats,
            isRanged: true, againstImage: false);
        AlignmentProtectionBenefits protection = AlignmentProtectionRules.GetBenefitsAgainst(target, attacker.Stats.CharacterAlignment);
        int targetAC = SpellCaster.TouchAttackAC(target, target.Stats, attacker.Stats, protection, out _);

        int roll = DiceService.D20(SpecialRangedTouchContext); // DiceService, so the scenario harness can force it
        int total = roll + terms.Total;
        bool hit = CombatCalculationService.IsHit(roll, total, targetAC);
        string natural = roll == 20 ? " (natural 20)" : roll == 1 ? " (natural 1)" : string.Empty;

        Debug.Log($"[AI] Ranged touch attack: d20({roll}) + {terms.Total} = {total} vs touch AC {targetAC}{natural} → {(hit ? "hit" : "miss")}");

        if (hit && SpellCaster.TouchAttackMissesByConcealment(attacker, target, true, out int missRoll, out int missChance))
        {
            Debug.Log($"[AI] Ranged touch attack misses due to concealment ({missRoll} ≤ {missChance}).");
            _gameManager?.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("🌫", $"{attacker.Stats.CharacterName}'s ranged touch attack misses {target.Stats.CharacterName} due to concealment ({missRoll} ≤ {missChance}%)."));
            return false;
        }

        return hit;
    }

    /// <summary>
    /// Roll damage for a special attack based on dice type and count.
    /// </summary>
    private int RollDamage(int damageDice, int damageCount)
    {
        // DiceService, so the scenario harness can force the dice (docs/TESTING.md 3.3)
        int total = damageCount > 0
            ? DiceService.RollMultiple(damageCount, Mathf.Max(2, damageDice), "Special attack damage")
            : 0;
        return Mathf.Max(1, total);
    }

    /// <summary>
    /// Apply a status effect from a special attack (blinding, sickening, etc.).
    /// Uses Fortitude or Will save depending on attack definition.
    /// Supports random duration via OnHitDurationDice.
    /// </summary>
    private void ApplyStatusEffectFromSpecialAttack(CharacterController target, RangedSpecialAttackDefinition attack,
        CharacterController source = null)
    {
        if (target == null || string.IsNullOrEmpty(attack.OnHitStatusEffectType))
            return;

        // Only apply if target fails a save (if DC > 0)
        if (attack.OnHitSaveDC > 0)
        {
            SavingThrowResolver.SaveResult saveResult;
            string saveTypeName;

            if (attack.OnHitSaveIsFortitude)
            {
                saveResult = SavingThrowResolver.ResolveFortitudeSave(target.Stats, attack.OnHitSaveDC, attack.OnHitStatusEffectType,
                    SaveContext.CreatureAbility(attack.OnHitStatusEffectType, source));
                saveTypeName = "Fort";
            }
            else
            {
                saveResult = SavingThrowResolver.ResolveWillSave(target.Stats, attack.OnHitSaveDC, attack.OnHitStatusEffectType,
                    SaveContext.CreatureAbility(attack.OnHitStatusEffectType, source));
                saveTypeName = "Will";
            }

            if (saveResult.Succeeded)
            {
                _gameManager.CombatUI?.ShowCombatLog(
                    CombatLogHelper.Success("💪", $"{target.Stats.CharacterName} resists {attack.OnHitStatusEffectType} ({saveTypeName} {saveResult.Total} vs DC {attack.OnHitSaveDC})!"));
                return;
            }
        }

        // Determine duration: random 1d(OnHitDurationDice) if set, otherwise fixed OnHitDurationRounds
        int duration = attack.OnHitDurationDice > 0
            ? Random.Range(1, attack.OnHitDurationDice + 1)
            : attack.OnHitDurationRounds;

        // Apply the effect
        switch (attack.OnHitStatusEffectType.ToLower())
        {
            case "blinded":
                target.ApplyBlindedCondition(duration, "Special Attack");
                _gameManager.CombatUI?.ShowCombatLog(
                    CombatLogHelper.Debuff("🌑", $"{target.Stats.CharacterName} is blinded for {duration} round(s)!"));
                break;
            
            case "sickened":
                target.ApplySickenedCondition(duration, "Special Attack");
                _gameManager.CombatUI?.ShowCombatLog(
                    CombatLogHelper.Debuff("🤢", $"{target.Stats.CharacterName} is sickened for {duration} round(s)!"));
                break;
            
            default:
                Debug.LogWarning($"[AI] Unknown status effect type: {attack.OnHitStatusEffectType}");
                break;
        }
    }

    /// <summary>
    /// The cast option of a grappling NPC's turn (PHB p.156; CMB-101, batch 12 step 19): a grappler may cast a spell of one
    /// standard action or less with no somatic component (no verbal one while pinned, CMB-080) and makes a Concentration
    /// check; the cast core's legality refuses every other cast (GameManager.CheckCastGrappleLegality, through
    /// CheckLegalityForAI, which ranks the candidates of <see cref="TryExecuteSpellcastAction"/>). AI stopgap (batch 13
    /// catalog): a spellcaster casts instead of grappling when it is the weaker grappler (its grapple modifier below the
    /// opponent's) or is pinned; otherwise it keeps grappling. The grapple opponent is the fallback target.
    /// </summary>
    private bool TryGrappledSpellcastForAI(CharacterController npc, CharacterController fallbackTarget)
    {
        if (npc == null || npc.Stats == null || !npc.Stats.IsSpellcaster || npc.Spellcasting == null)
            return false;
        if (!npc.TryGetGrappleState(out CharacterController opponent, out _, out bool pinned, out _) || opponent == null)
            return false;
        bool weaker = npc.GetGrappleModifier() < opponent.GetGrappleModifier();
        if (!weaker && !pinned)
            return false;
        // No score floor: a floor of 0 (as the dragon's cast-first step has) refused the only legal cast, Blindness/Deafness
        // on the grappler, on every seed of rules/grappled-npc-casts-legal-only (batch 12 step 19 review), so the strategist's
        // scores are not calibrated to 0 for a grappled caster; any legal candidate replaces the grapple actions (AI stopgap).
        bool cast = TryExecuteSpellcastAction(npc, opponent ?? fallbackTarget);
        Debug.Log($"[AI][Grapple] {npc.Stats.CharacterName} {(cast ? "casts" : "finds no legal cast")} while grappling (weaker={weaker}, pinned={pinned}).");
        return cast;
    }

    /// <summary>
    /// Runs the grapple-restricted turn. If the NPC ended the grapple without spending its standard action
    /// (releasing a pin is a free action that ends the grapple, PHB p.157, CMB-089) and is prone, it stands
    /// up here, because the stand-up gate in ExecuteNPCTurn skips grappling creatures (CMB-074).
    /// </summary>
    private IEnumerator RunGrappleTurnForAI(CharacterController npc)
    {
        yield return _gameManager.StartCoroutine(_gameManager.ExecuteGrappleRestrictedTurnForAI(npc));

        if (CanContinueTurnAfterGrappleEnded(npc) && npc.HasCondition(CombatConditionType.Prone))
            yield return _gameManager.StartCoroutine(_gameManager.TryStandUpFromProneForAI(npc));

        if (CanContinueTurnAfterGrappleEnded(npc))
            Debug.Log($"[AI][Grapple] {npc.Stats.CharacterName} is no longer grappling and still has its standard action; continuing the turn.");
    }

    /// <summary>
    /// Releasing a pin is a free action that ends the grapple (PHB p.157, CMB-089). A PC keeps its
    /// standard and move actions afterwards, so an NPC whose grapple ended this turn without spending
    /// its standard action carries on with the normal turn instead of ending it.
    /// </summary>
    private bool CanContinueTurnAfterGrappleEnded(CharacterController npc)
    {
        return npc != null
            && !npc.IsGrappling()
            && !ThreatSystem.IsMoverIncapacitated(npc)
            && npc.Actions != null
            && npc.Actions.HasStandardAction
            && _gameManager.CurrentPhase != GameManager.TurnPhase.CombatOver;
    }
}
