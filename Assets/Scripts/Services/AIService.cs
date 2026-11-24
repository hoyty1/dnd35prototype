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
        /// <summary>Unused since batch 14 step 9: SelectBestAction no longer returns it (the maneuver choice is DND35.AI.AiManeuverChooser, AI-027).</summary>
        SpecialManeuver,
        Charge,
        Retreat
    }

    private GameManager _gameManager;

    private System.Func<AIDifficultyLevel> _difficultySource;

    /// <summary>
    /// An override of the game setting (batch 14 step 4): null reads GameSettings.aiDifficulty live. The scenario harness
    /// sets it per job and gives the saved value (null included) back at teardown; nothing else writes it.
    /// </summary>
    public AIDifficultyLevel? DifficultyOverride { get; set; }

    /// <summary>
    /// The AI difficulty (batch 14 step 4, C11, Q6 default; AI-012): <see cref="DifficultyOverride"/> when set, else
    /// GameSettings.aiDifficulty read live (default Normal; no menu writes it yet, and a future menu writes that field),
    /// else Normal. Setting it sets the override. Read by the personality blend only, through
    /// DND35.AI.Personality.AiBlend.SettingsDifficulty, which <see cref="Initialize"/> points here: Easy skips the opinion
    /// rows marked MinDifficulty Normal (Sadistic's coup de grace Insist). A creature with no personality plays the same
    /// at every difficulty; Hard equals Normal in batch 14.
    /// </summary>
    public AIDifficultyLevel Difficulty
    {
        get
        {
            if (DifficultyOverride.HasValue)
                return DifficultyOverride.Value;
            GameSettings settings = GameSettings.Instance;
            return settings != null ? settings.aiDifficulty : AIDifficultyLevel.Normal;
        }
        set => DifficultyOverride = value;
    }

    /// <summary>
    /// When on, every finished AI turn of <see cref="AiDecisionTrace"/> is also one collapsed "[AI-TRACE]" Debug.Log line
    /// (batch 14 step 1). Off by default; the trace's ring and counts are kept either way in the editor and development
    /// builds. Never a combat-log line, so no scenario trace hash sees it.
    /// </summary>
    public static bool TraceEnabled { get; set; }

    /// <summary>
    /// The creatures that cast and held a touch charge this turn to move and touch (a HoldCharge candidate, batch 11 step
    /// 27), with the foe they meant to touch: the turn's end moves them into reach and delivers it.
    /// </summary>
    private readonly Dictionary<CharacterController, CharacterController> _heldChargePlans = new Dictionary<CharacterController, CharacterController>();

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
        // The personality blend reads the game's difficulty here (batch 14 step 4); read lazily, so GameSettings is
        // only touched when a personality decides.
        _difficultySource = () => Difficulty;
        DND35.AI.Personality.AiBlend.SettingsDifficulty = _difficultySource;
    }

    public void Cleanup()
    {
        _gameManager = null;
        if (_difficultySource != null && DND35.AI.Personality.AiBlend.SettingsDifficulty == _difficultySource)
            DND35.AI.Personality.AiBlend.SettingsDifficulty = null;
        _difficultySource = null;
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
    /// run time), not a dragon (its own routine), not DefensiveMelee, otherwise a ranged behaviour or a Ranged
    /// <see cref="AIProfile.CombatStyle"/>; without a profile, a ranged behaviour. The ranged behaviours are RangedKiter
    /// and Ranged ("prefer ranged attacks"), which since batch 13 step 8 runs the same ranged routine and shoots, with or
    /// without a profile (AI-003). The spawn uses the same test to decide which weapon a creature starts holding
    /// (<see cref="Inventory.EquipStartingLoadout"/>, ITM-004).
    /// </summary>
    internal static bool RoutesToRangedTurn(NPCAIBehavior behavior, AIProfile profile)
    {
        // See RunsRangedRoutine for the turn's extra test on the Ranged behaviour (a creature with no ranged means).
        bool rangedBehavior = behavior == NPCAIBehavior.RangedKiter || behavior == NPCAIBehavior.Ranged;
        if (profile == null)
            return rangedBehavior;
        if (profile is HealerAIProfile)
            return true;
        if (profile is DragonAIProfile)
            return false;
        if (behavior == NPCAIBehavior.DefensiveMelee)
            return false;
        return rangedBehavior || profile.CombatStyle == CombatStyle.Ranged;
    }

    /// <summary>
    /// The turn's routing test: <see cref="RoutesToRangedTurn"/>, except that a creature routed there only by the Ranged
    /// behaviour ("prefers ranged attacks"; AI-003, batch 13 step 8) that has no ranged means now
    /// (<see cref="HasRangedMeans"/>: no ranged weapon that can shoot, no throwable weapon, no castable spell beyond touch)
    /// runs the aggressive melee routine, with its charge and its standard-action ranged specials. Such creatures are the
    /// gauth, beholder and rakshasa, whose eye rays and spells are text only (CRE-017), and an archer whose
    /// quiver is empty (AI-073). An AI choice, not a rule: preferring ranged attacks means nothing without one.
    /// </summary>
    private bool RunsRangedRoutine(CharacterController npc, CharacterController target, NPCAIBehavior behavior, AIProfile profile)
    {
        if (!RoutesToRangedTurn(behavior, profile))
            return false;
        bool onlyByRangedBehavior = behavior == NPCAIBehavior.Ranged
            && !(profile is HealerAIProfile)
            && (profile == null || profile.CombatStyle != CombatStyle.Ranged);
        if (onlyByRangedBehavior && !HasRangedMeans(npc, target))
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} prefers ranged attacks but has no ranged means: aggressive melee routine (AI-003)");
            return false;
        }
        return true;
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
            BeginDecisionRecords(npc);
        }

        yield return _gameManager != null ? _gameManager.StartCoroutine(ExecuteNPCTurnBody(npc, behavior)) : null;

        // Reference checks for the bookkeeping: a summon slain during its own turn leaves play at once and Unity destroys it
        // at the end of that frame (CRE-312), so Unity's null check would skip closing its records depending on frames.
        if (!ReferenceEquals(npc, null) && _heldChargePlans.TryGetValue(npc, out CharacterController plannedTarget))
        {
            _heldChargePlans.Remove(npc);
            if (npc != null && !npc.IsRemovedFromPlay && !CombatEnded() && npc.Stats != null && npc.CanTakeTurnActions())
                yield return _gameManager.StartCoroutine(TryDeliverHeldChargeTurn(npc, plannedTarget));
        }

        if (!ReferenceEquals(npc, null))
        {
            AiDecisionTimer.EndTurn(npc);
            AiDecisionTrace.EndTurn(npc);
        }
    }

    /// <summary>
    /// Opens the turn's decision-time record and decision-trace record (batch 14 step 1), keyed by the actor so re-entrant
    /// turns (CORE-012) do not mix. Reads only; a no-op outside the editor and development builds.
    /// </summary>
    private void BeginDecisionRecords(CharacterController npc)
    {
        if (!AiDecisionTimer.Enabled && !AiDecisionTrace.Enabled)
            return;
        string name = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
        int round = _gameManager != null ? _gameManager.CurrentRound : 0;
        AiDecisionTimer.BeginTurn(npc, name, round);
        AIProfile profile = GetProfile(npc);
        AiDecisionTrace.BeginTurn(npc, name, round, profile != null ? profile.ProfileName : "none", string.Empty);
    }

    /// <summary>The trace's name for a chosen creature (batch 14 step 1).</summary>
    private static string TraceName(CharacterController c)
        => c == null ? "none" : c.Stats != null ? c.Stats.CharacterName : c.name;

    /// <summary>
    /// The creature whose turn-start prone choice (ChooseProneStartForAI) kept it down this turn (StayProne, or a crawl that
    /// was made), else null; reset at every AI turn's start. The move after the standard action reads it so a KeepDistance
    /// wish does not stand it up again (BuildAfterStandardActionContext, AfterStandardActionContext.StayingProne).
    /// </summary>
    private CharacterController _stayedProneThisTurn;

    private IEnumerator ExecuteNPCTurnBody(CharacterController npc, NPCAIBehavior behavior)
    {
        _stayedProneThisTurn = null;
        if (_gameManager == null || npc == null || npc.Stats == null || CombatEnded())
            yield break;

        // The turn-start effects, resets and perception already ran (GameManager.OnTurnStarted, SingleNPCTurnFromInitiative).

        bool isSummon = _gameManager.IsSummonedCreature(npc);
        string turnColor = isSummon ? "#66E8FF" : "#FF6666";
        string turnIcon = isSummon ? "✶" : "💀";

        _gameManager.CombatUI?.SetTurnIndicator($"{_gameManager.GetSummonDisplayName(npc)}'s turn...");
        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Summon("", $"<color={turnColor}>{turnIcon} {_gameManager.GetSummonDisplayName(npc)}'s turn begins</color>"));
        yield return CombatPacing.Wait(0.6f);
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
            yield return CombatPacing.Wait(0.25f);
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
        // flight, the Animate Rope escape, the no-target search, the grapple turn, the free spittle
        // and the Resilient Sphere restriction. The harness returns null for Frightened, Panicked or Turned
        // actors, so the forced kinds stay with the AI, and for any actor it wants the AI to run
        // (ScenarioHooks.ScriptedTurn).
        IEnumerator scripted = ScenarioHooks.ScriptedTurn?.Invoke(npc);
        if (scripted != null)
        {
            // A scripted turn makes no AI decision: it is not an AI turn sample (batch 14 step 1).
            AiDecisionTimer.CancelTurn(npc);
            AiDecisionTrace.CancelTurn(npc);
            yield return _gameManager.StartCoroutine(scripted);
            yield break;
        }

        // ── Prone at the turn start (batch 14 step 13, D5, C8) ──
        // Standing up is a move action that provokes (PHB p.141, Table 8-2; CMB-074), crawling 5 feet a move action that
        // provokes (PHB p.142), and nothing makes a prone creature stand: the Role's AIProfile.ChooseProneStart decides. By
        // default it stands, through the same resolution as the PC Stand Up button, unless it is unthreatened, its planned
        // action works prone (a cast or a crossbow shot, PHB p.311) and a foe in view fights at range. Grappling creatures
        // stay down (grapple turn below); a frightened, panicked or turned creature stands for its flight, as before.
        if (npc.HasCondition(CombatConditionType.Prone) && !npc.IsGrappling())
        {
            Vector2Int? crawlSquare = null;
            ProneStartChoice proneChoice = compulsion.Fear == null && !compulsion.Turned
                ? ChooseProneStartForAI(npc, out crawlSquare)
                : ProneStartChoice.StandUp;
            if (proneChoice == ProneStartChoice.Crawl && crawlSquare.HasValue)
            {
                ActionContext crawlCtx = ActionContext.For(ActionIds.Crawl, npc, ActorSource.AI);
                crawlCtx.Cell = crawlSquare.Value;
                var crawl = new ActionOutcome();
                yield return _gameManager.StartCoroutine(_gameManager.RunAIAction(crawlCtx, crawl));
                if (CombatEnded())
                    yield break;
                if (ThreatSystem.IsMoverIncapacitated(npc))
                {
                    Debug.Log($"[AI] {npc.Stats.CharacterName} was dropped by an AoO while crawling — turn ended");
                    yield break;
                }
                if (crawl.Performed)
                {
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} crawls 5 feet and stays prone."));
                    _stayedProneThisTurn = npc;
                }
                else
                {
                    // A refused crawl spends nothing (ActionCore_Crawl: a barrier, an invalid square, a flight rule): the
                    // creature stands up instead, as before the step, so its move action is not left unused while prone.
                    Debug.Log($"[AI][ProneStart] {npc.Stats.CharacterName}: crawl refused ({crawl.Refusal.Reason}); stands up instead");
                    proneChoice = ProneStartChoice.StandUp;
                }
            }
            else if (proneChoice == ProneStartChoice.StayProne)
            {
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} stays prone and fights from the ground."));
                _stayedProneThisTurn = npc;
            }
            if (proneChoice == ProneStartChoice.StandUp || (proneChoice == ProneStartChoice.Crawl && !crawlSquare.HasValue))
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

        // Rage (PHB p.25, D10): entering a rage takes no time but only on the barbarian's own turn, so a barbarian decides it
        // at the start of its turn (design choice C6, AI-079: a foe within reach or charge range), through the executor the
        // PC's Rage button runs; the turn goes on.
        _gameManager.TryNPCRageForAI(npc);

        // The Strength domain's feat of strength (PHB p.188, D7, C14): a free action that lasts the round. At the turn start
        // only for a grappling cleric, whose grapple checks use Strength (PHB p.156); every other turn decides it where the
        // melee attack is certain (the melee attack action, the first swing, the charge's attack; batch 14 step 14 review),
        // so a turn that then aids, defends, cures or retreats does not waste the day's use. Only a cleric with Strength,
        // Destruction or Death left gets this far into it.
        _gameManager.TryNPCDomainPowerForAI(npc, DomainPowerSite.TurnStart);

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
            yield return CombatPacing.Wait(0.35f);
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

        // The weapon fallback (batch 13 step 15, AI-073): a creature that holds no weapon it can fight with (empty hands
        // after a throw or a disarm, a bow with an empty quiver) draws one through the catalog's draw entry at its RAW cost,
        // before its routine reads the weapon in hand (AIProfile.EnsureWeapon).
        if (!npc.IsGrappling())
            AIProfile.EnsureWeapon(npc);

        if (npc.IsGrappling())
        {
            // A grappling caster may keep its spell up (ConcentrationRules; delegated RAW reading 2026-10-15, PHB p.156,
            // p.176): the same choice as below, against a grapple attack as the other standard action.
            if (TryMaintainConcentrationTurn(npc, targetPC, grappling: true))
            {
                yield return CombatPacing.Wait(0.5f);
                if (!KeepsRoutineAfterAbility(npc))
                {
                    yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, StyleWithoutProfile(behavior)));
                    yield break;
                }
            }
            // A grappler may cast a spell the grapple allows (PHB p.156; CMB-101): the cast candidates of the ordinary turn,
            // each through the shared cast legality (CheckLegalityForAI meets GrappleSpellcastRefusal), in place of its grapple
            // actions.
            if (TryGrappledSpellcastForAI(npc, targetPC))
            {
                yield return CombatPacing.Wait(0.5f);
                if (!KeepsRoutineAfterAbility(npc))
                {
                    yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, StyleWithoutProfile(behavior)));
                    yield break;
                }
            }
            yield return _gameManager.StartCoroutine(RunGrappleTurnForAI(npc));
            if (CombatEnded() || !CanContinueTurnAfterGrappleEnded(npc)) yield break; // free pin release (CMB-089)
        }

        // The supernatural aura (gibbering, babble, moan) fired at the start of this acting turn, for every bearer and every
        // turn, compelled ones included (GameManager.ProcessTurnStartAura from BeginActingTurn; batch 13 step 19, D13, AI-053).

        // ── Free-Action Ranged Special Attack (Spittle, etc.) ──
        // Per MM p.126, Gibbering Mouther's Spittle is a free action each round, at one opponent within its range.
        // This does NOT consume a standard or move action — the creature still acts normally. The spittle is a choice, so a
        // compelled turn (the branches above) never reaches it (delegated RAW reading 2026-10-29, D13).
        if (npc.HasRangedSpecialAttack)
        {
            RangedSpecialAttackDefinition freeAttack = npc.GetRangedSpecialAttackDefinition();
            // The spittle entry's legality first (batch 14 step 10, AI-080): a nauseated mouther is unable to attack (PHB
            // p.310; free actions remain, but the spittle is a ranged touch attack, MM p.126), so it picks no spittle its core
            // would refuse.
            if (freeAttack != null && freeAttack.IsFreeAction && npc.IsRangedSpecialAttackReady
                && AiLegal.CanUse(npc, ActionIds.Spittle, _gameManager))
            {
                CharacterController spittleTarget = FindClosestEnemyInRange(npc, freeAttack.RangeFeet);
                if (spittleTarget != null)
                {
                    Debug.Log($"[AI] {npc.Stats.CharacterName} fires free-action {freeAttack.Name} at {spittleTarget.Stats.CharacterName}");
                    TryExecuteRangedSpecialAttack(npc, spittleTarget);
                    if (CombatEnded()) // the core ran the combat-end check (batch 13 step 18 review)
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
            yield return CombatPacing.Wait(0.5f);
            yield break;
        }

        AIProfile profile = GetProfile(npc);
        if (profile is SwarmAI swarmProfile)
        {
            yield return _gameManager.StartCoroutine(ExecuteSwarmTurn(npc, swarmProfile, profile is IndiscriminateSwarmAI));
            yield break;
        }

        // Lay on Hands (Su, PHB p.44): healing a hurt creature within touch, or destroying an undead foe the pool can
        // finish, with the standard action, through the rules the PC button calls (LayOnHandsRules, CHR-020). Its move
        // action follows (UseMoveAfterStandardAction, batch 14 step 11, AI-077: a standard and a move action, PHB p.138).
        if (!isSummon && _gameManager.TryNPCLayOnHandsTurnForAI(npc))
        {
            yield return CombatPacing.Wait(0.5f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, StyleWithoutProfile(behavior)));
                yield break;
            }
        }

        // Turn undead (Su, PHB p.159): a cleric or paladin turns when a hostile turnable undead is within 60 ft with line of
        // effect and its best check could affect the nearest (design choice C6, AI-054), through the executor the PC's Turn
        // Undead button runs (D9). Its move action follows, as after lay on hands (batch 14 step 11, AI-077).
        if (!isSummon && _gameManager.TryNPCTurnUndeadTurnForAI(npc))
        {
            yield return CombatPacing.Wait(0.5f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, StyleWithoutProfile(behavior)));
                yield break;
            }
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
        // the PC Bardic Music button calls (BardicMusicRules, CHR-026), when AIProfile.ChooseBardicMusic picks one; its move
        // action follows (batch 14 step 11, AI-077). Stopping a song costs no action, and the
        // turn goes on (to cast while Inspire Courage lingers).
        if (_gameManager.TryNPCBardicMusicTurnForAI(npc))
        {
            yield return CombatPacing.Wait(0.5f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, StyleWithoutProfile(behavior)));
                yield break;
            }
        }

        // A maintained spell (PHB p.176): keeping it going is this turn's standard action, through the rule the PC's
        // Maintain button calls (ConcentrationRules, decision D2, SPL-153), when AIProfile.ShouldMaintainConcentration
        // finds it worth more than the other standard action; its move action follows, as after a song (batch 14 step 11,
        // AI-077). Otherwise the turn goes on, and a cast or the end of the turn ends the spell.
        if (TryMaintainConcentrationTurn(npc, targetPC))
        {
            yield return CombatPacing.Wait(0.5f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, StyleWithoutProfile(behavior)));
                yield break;
            }
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
                // Healing only with a castable cure (batch 14 step 12, AI-063): without one the healer fights instead.
                bool hasCastableCure = hasCastableSpells && HealerAIProfile.HasCastableCure(npc, _gameManager);
                HealerActionType actionType = healerProfile.DetermineActionPriority(npc, allCombatants, hasCastableSpells, hasCastableCure);
                // One decision-trace row (outside the hash): the priority, its legacy the one before AI-063 (no cure test).
                if (AiDecisionTrace.HasOpenTurn(npc))
                    AiDecisionTrace.Record(npc, "HealerPriority", actionType.ToString(),
                        healerProfile.DetermineActionPriority(npc, allCombatants, hasCastableSpells, hasCastableCure: true).ToString(), 0f,
                        $"castableSpells={hasCastableSpells} castableCure={hasCastableCure}");

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
                        // The cure first (batch 14 step 12, AI-047): a Healing-effect spell on a hurt ally in reach.
                        bool healCasted = TryExecuteSpellcastAction(npc, healTarget, IsHealingEffectSpell);
                        if (!healCasted)
                        {
                            // No patient in reach: the Role's approach (AIProfile.ChooseHealerApproach, C8, Q8).
                            var approach = new HealerApproachOutcome();
                            yield return _gameManager.StartCoroutine(RunHealerApproach(npc, healerProfile, healTarget, approach));
                            if (approach.TurnDone || CombatEnded() || npc.Stats == null || !npc.CanTakeTurnActions())
                                yield break;
                            if (approach.Moved)
                            {
                                // The move is spent; the kiter uses the standard action that is left.
                                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                                yield break;
                            }
                            // Today's turn: the spell walk over every castable spell.
                            healCasted = TryExecuteSpellcastAction(npc, healTarget);
                        }
                        if (healCasted)
                        {
                            Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} successfully healed {healTarget.Stats.CharacterName}.");
                            yield return CombatPacing.Wait(0.5f);
                            // The move action after the cure (batch 14 step 11, AI-077); a quickened cure keeps the routine going.
                            if (!KeepsRoutineAfterAbility(npc))
                            {
                                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, CombatStyle.Ranged));
                                yield break;
                            }
                            Debug.Log($"[AI][Healer] {npc.Stats.CharacterName}: quickened or free cure, the routine continues with the ranged kiter.");
                        }
                        else
                        {
                            Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} failed to heal — falling back to ranged kiter.");
                        }
                    }
                }

                if (actionType == HealerActionType.Buffing)
                {
                    // T1.1 FIX: Actually attempt to cast buff spell (ally targeting now works)
                    bool buffCasted = TryExecuteSpellcastAction(npc, null);
                    if (buffCasted)
                    {
                        Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} successfully cast buff spell.");
                        yield return CombatPacing.Wait(0.5f);
                        // The move action after the buff (batch 14 step 11, AI-077).
                        if (!KeepsRoutineAfterAbility(npc))
                        {
                            yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, targetPC, CombatStyle.Ranged));
                            yield break;
                        }
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
            else if (RunsRangedRoutine(npc, targetPC, behavior, profile))
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
            case NPCAIBehavior.Ranged: // prefers ranged attacks: the ranged routine when it has ranged means (AI-003; RunsRangedRoutine)
                if (RunsRangedRoutine(npc, targetPC, behavior, null))
                    yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                else
                    yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
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
            _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, searchDestination, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, searchMove)));
        if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
        if (!npc.CanTakeTurnActions())
            yield break; // dropped by an AoO or area damage while moving (CMB-073)
        if (searchMove.Refused)
            yield break; // refused before the first step (no path, or the shared legality): nothing spent (D3)
        // The move executor spent the move action before the first step (D3).

        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npcName} moves to search for enemies."));
        yield return CombatPacing.Wait(0.35f);
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
        yield return CombatPacing.Wait(0.35f);
    }

    /// <summary>
    /// After a standard action that is not an attack (a wild shape), the move action: toward the best target when it
    /// is out of reach, by the same movement choice as the aggressive melee turn. Nothing else this turn.
    /// </summary>
    private IEnumerator MoveTowardTargetAfterStandardAction(CharacterController npc)
    {
        if (CombatEnded() || npc == null || npc.Stats == null || !npc.CanTakeTurnActions() || npc.Actions == null || !AiLegal.CanMoveNow(npc, _gameManager))
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
            _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, advanceMove)));
        if (CombatEnded() || !npc.CanTakeTurnActions() || advanceMove.Refused)
            yield break; // a refused move spends nothing (D3); the executor spent this one before the first step
        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!"));
        yield return CombatPacing.Wait(0.5f);
    }

    // ── The move left after a standard-action ability (batch 14 step 11, AI-077; decision D1, design choice C8) ──

    /// <summary>
    /// True when the routine goes on after a standard-action ability: the creature can still act and its standard action
    /// is left, because the cast was quickened or a free action (PHB p.98: a free action, one per round; D1). The routine
    /// then takes its next step as if nothing had been spent; otherwise the site runs <see cref="UseMoveAfterStandardAction"/>.
    /// A touch spell cast to hold its charge ends the routine even when quickened: the turn's delivery
    /// (TryDeliverHeldChargeTurn, SPL-115) needs the move and standard actions, and another cast would dissipate the
    /// charge (PHB p.176).
    /// </summary>
    private bool KeepsRoutineAfterAbility(CharacterController npc)
        => !CombatEnded() && npc != null && npc.Stats != null && npc.CanTakeTurnActions() && npc.Actions != null
           && npc.Actions.HasStandardAction && !_heldChargePlans.ContainsKey(npc);

    /// <summary>
    /// The pure reads of AIProfile.ChooseAfterStandardAction for <paramref name="npc"/> (batch 14 step 11, C8): whether it
    /// may move (AiLegal.CanMoveNow; a prone creature, not grappling, the stand-up entry with its move action left),
    /// whether <paramref name="focus"/> is out of the reach or range of the weapon in hand, whether a foe is within the
    /// keep-away distance (the kiter's 2 squares, AiBlend.KeepAwayDistance) or threatens its square, and whether it can
    /// still fight from range (AI-039). <paramref name="nearestFoe"/> is the closest living foe, whom a KeepDistance
    /// backs away from. <paramref name="styleWithoutProfile"/> is the routine's style for a creature with no profile.
    /// </summary>
    internal AfterStandardActionContext BuildAfterStandardActionContext(CharacterController npc, CharacterController focus,
        CombatStyle styleWithoutProfile, out CharacterController nearestFoe)
    {
        nearestFoe = null;
        var ctx = new AfterStandardActionContext();
        if (npc == null || npc.Stats == null || npc.Actions == null || _gameManager == null)
            return ctx;
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Action, npc);
        AIProfile profile = GetProfile(npc);
        ctx.Style = profile != null ? profile.CombatStyle : styleWithoutProfile;
        ctx.CastingRole = profile != null && profile.IsCastingProfile;
        ctx.Prone = npc.HasCondition(CombatConditionType.Prone) && !npc.IsGrappling();
        ctx.StayingProne = ctx.Prone && _stayedProneThisTurn == npc;
        ctx.CanMove = ctx.Prone
            ? npc.Actions.HasMoveAction && AiLegal.CanUse(npc, ActionIds.StandUp, _gameManager)
            : AiLegal.CanMoveNow(npc, _gameManager) && !npc.Stats.MovementBlockedByCondition;
        if (!ctx.CanMove)
            return ctx;

        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        ctx.FocusOutOfReach = focus != null && focus.Stats != null && !focus.IsDead && !npc.IsTargetInCurrentWeaponRange(focus)
            // A focus the creature has turned is no approach target: approaching within 10 feet ends the turning (PHB p.159).
            && !_gameManager.IsTurnedByForAI(focus, npc);
        int keepAway = DND35.AI.Personality.AiBlend.KeepAwayDistance(npc, DND35.AI.Personality.AiBlend.LegacyKeepAwaySquares,
            DND35.AI.Personality.AiBlend.IsRangedOrCastingRole(profile));
        int nearest = int.MaxValue;
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController foe = all[i];
            if (foe == null || foe == npc || foe.Stats == null || foe.IsDead || !foe.CanTakeTurnActions() || !_gameManager.IsEnemyTeamForAI(npc, foe))
                continue;
            int d = SquareGridUtils.GetDistance(npc.GridPosition, foe.GridPosition);
            if (d < nearest)
            {
                nearest = d;
                nearestFoe = foe;
            }
        }
        ctx.FoeWithinKeepAway = nearestFoe != null && nearest <= keepAway;
        ctx.Threatened = ThreatSystem.IsSquareThreatened(npc.GridPosition, npc, all);
        if (ctx.FoeWithinKeepAway || ctx.Threatened)
            ctx.CanFightFromRange = CanFightFromRange(npc, focus != null && !focus.IsDead ? focus : nearestFoe, all);
        return ctx;
    }

    /// <summary>
    /// The move action after a standard-action ability (batch 14 step 11, AI-077; D1, C8), which replaced the wait and the
    /// end of the turn after a cast, a song, a maintained spell, Lay on Hands, Turn Undead and the healer's cure and buff:
    /// the Role's AIProfile.ChooseAfterStandardAction (AIProfile.DefaultChooseAfterStandardAction without a profile),
    /// tilted by the traits (AiBlend.AfterStandardAction). Approach moves to the reach or range of the weapon in hand from
    /// <paramref name="focus"/> (AiOutOfReach.ApproachRangeSquares), KeepDistance away from the nearest foe, both chosen
    /// by EvaluateMovementOptions (its paths priced by AiBlend.AoOCost) and run through the catalog's move entry
    /// (RunAIAction), after AiLegal.CanMoveNow; StandUp runs the stand-up entry. A destination equal to the creature's own
    /// square is no move. Called only when the standard action is spent (<see cref="KeepsRoutineAfterAbility"/>).
    /// </summary>
    private IEnumerator UseMoveAfterStandardAction(CharacterController npc, CharacterController focus, CombatStyle styleWithoutProfile)
    {
        if (CombatEnded() || npc == null || npc.Stats == null || !npc.CanTakeTurnActions() || npc.Actions == null)
            yield break;
        // A touch spell cast to hold its charge this turn keeps the move for its delivery (ExecuteNPCTurn's
        // TryDeliverHeldChargeTurn moves into reach of the planned target and touches it, SPL-115).
        if (_heldChargePlans.ContainsKey(npc))
            yield break;

        AfterStandardActionContext ctx = BuildAfterStandardActionContext(npc, focus, styleWithoutProfile, out CharacterController nearestFoe);
        AIProfile profile = GetProfile(npc);
        AfterStandardActionChoice roleChoice = profile != null
            ? profile.ChooseAfterStandardAction(npc, ctx)
            : AIProfile.DefaultChooseAfterStandardAction(ctx);
        AfterStandardActionChoice choice = DND35.AI.Personality.AiBlend.AfterStandardAction(npc, roleChoice, ctx);
        if (AiDecisionTrace.HasOpenTurn(npc))
            AiDecisionTrace.Record(npc, "AfterStandard", choice.ToString(), roleChoice.ToString(), 0f,
                ctx.CanMove ? $"outOfReach={ctx.FocusOutOfReach} near={ctx.FoeWithinKeepAway} threatened={ctx.Threatened}" : "no move");
        string name = npc.Stats.CharacterName;
        Debug.Log($"[AI][AfterStandard] {name}: {choice} (Role {roleChoice}; move={ctx.CanMove} prone={ctx.Prone} outOfReach={ctx.FocusOutOfReach} near={ctx.FoeWithinKeepAway} threatened={ctx.Threatened} ranged={ctx.CanFightFromRange})");

        // Standing up from prone provokes from every threatener (PHB p.141, Table 8-2): a creature whose traits Forbid
        // provoking stays down rather than stand into the AoOs (C6, the move's price by AiBlend.AoOCost).
        if (choice == AfterStandardActionChoice.StandUp && ctx.Threatened
            && DND35.AI.Personality.AiBlend.AoOCost(npc, AvoidsAoOs(npc, profile)
                ? DND35.AI.Personality.AiBlend.AoOForbidPrice
                : DND35.AI.Personality.AiBlend.AoOForCeiling) >= DND35.AI.Personality.AiBlend.AoOForbidPrice)
        {
            Debug.Log($"[AI][AfterStandard] {name} stays prone: standing up would provoke and it never provokes.");
            yield break;
        }

        switch (choice)
        {
            case AfterStandardActionChoice.StandUp:
            {
                var stand = new ActionOutcome();
                yield return _gameManager.StartCoroutine(_gameManager.RunAIAction(ActionContext.For(ActionIds.StandUp, npc, ActorSource.AI), stand));
                yield break;
            }

            case AfterStandardActionChoice.Approach:
            case AfterStandardActionChoice.KeepDistance:
            {
                bool approach = choice == AfterStandardActionChoice.Approach;
                CharacterController from = approach ? focus : nearestFoe;
                if (from == null || from.Stats == null || !AiLegal.CanMoveNow(npc, _gameManager))
                    yield break;
                // A move that ends within 10 feet of an undead this creature turned ends that turning (PHB p.159; the
                // move's proximity check, CMB-262): such a destination is refused, so the move never undoes the turning.
                Func<Vector2Int, bool> breaksTurning = c => _gameManager.WouldBreakTurningAtForAI(npc, c);
                // A threatened creature backing away first tries a 5-foot step out of every threat (PHB p.144: it has not
                // moved this round, and the step provokes no AoO), then the priced move (AI-088).
                if (!approach && ctx.Threatened && TryFiveFootStepOutOfThreat(npc, from, breaksTurning))
                {
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} takes a 5-foot step away from {from.Stats.CharacterName} to keep its distance."));
                    yield return CombatPacing.Wait(0.35f);
                    yield break;
                }
                SquareCell cell = approach
                    ? EvaluateMovementOptions(npc, from.GridPosition, retreat: false, from, profile, AiOutOfReach.ApproachRangeSquares(npc), breaksTurning)
                    : EvaluateMovementOptions(npc, from.GridPosition, retreat: true, profile: profile, rejectCell: breaksTurning);
                if (cell == null || cell.Coords == npc.GridPosition)
                    yield break;
                ActionContext moveCtx = ActionContext.For(ActionIds.Move, npc, ActorSource.AI);
                moveCtx.Cell = cell.Coords;
                var move = new ActionOutcome();
                yield return _gameManager.StartCoroutine(_gameManager.RunAIAction(moveCtx, move));
                if (CombatEnded() || !move.Performed || npc.Stats == null || !npc.CanTakeTurnActions())
                    yield break;
                _gameManager.CombatUI?.ShowCombatLog(approach
                    ? CombatLogHelper.Info("", $"{name} advances toward {from.Stats.CharacterName}.")
                    : CombatLogHelper.Info("", $"{name} backs away from {from.Stats.CharacterName} to keep its distance."));
                yield return CombatPacing.Wait(0.5f);
                yield break;
            }
        }
    }

    /// <summary>
    /// The 5-foot step of a threatened creature that keeps its distance after a standard action (batch 14 step 11, AI-088):
    /// the adjacent square the step rule allows (CanTakeFiveFootStepToForAI: no move this round, PHB p.144), that no foe
    /// threatens and that <paramref name="rejectCell"/> does not refuse, farthest from <paramref name="from"/>; taken
    /// through the catalog's 5-foot step entry. False, and nothing spent, when there is none.
    /// </summary>
    private bool TryFiveFootStepOutOfThreat(CharacterController npc, CharacterController from, Func<Vector2Int, bool> rejectCell)
    {
        if (npc == null || from == null || _gameManager == null || _gameManager.Grid == null || !_gameManager.CanTakeFiveFootStepForAI(npc))
            return false;
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        bool found = false;
        Vector2Int best = npc.GridPosition;
        int bestDist = int.MinValue;
        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(npc.GridPosition);
        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int candidate = neighbors[i];
            if (!_gameManager.CanTakeFiveFootStepToForAI(npc, candidate))
                continue;
            if (rejectCell != null && rejectCell(candidate))
                continue;
            if (ThreatSystem.IsSquareThreatened(candidate, npc, all))
                continue;
            int d = SquareGridUtils.GetDistance(candidate, from.GridPosition);
            if (d > bestDist)
            {
                bestDist = d;
                best = candidate;
                found = true;
            }
        }
        return found && _gameManager.TryTakeFiveFootStepForAI(npc, best);
    }

    // ── Aid another, total defense and posture (batch 14 step 13, AI-063, batch 13's C5; decisions D3-D5, design choice C8) ──

    /// <summary>The target's AC as <paramref name="npc"/> knows it (the AcEstimate fact), else its AC.</summary>
    private static int KnownAC(CharacterController npc, CharacterController target)
    {
        DND35.AI.Knowledge.ITargetView view = DND35.AI.Knowledge.KnowledgeService.ViewOf(npc, target);
        DND35.AI.Knowledge.FactValue ac = view != null ? view.Get(DND35.AI.Knowledge.FactKey.AcEstimate) : DND35.AI.Knowledge.FactValue.Unknown;
        return ac.IsKnown ? ac.Number : target.Stats.ArmorClass;
    }

    /// <summary>
    /// <paramref name="c"/>'s melee attack modifier against <paramref name="target"/> for the AI's estimates: with a melee
    /// weapon in the main hand the shared attack terms (CharacterController.BuildAttackBonus, CMB-043) plus its enhancement
    /// or masterwork bonus, as the roll adds them; else its melee attack estimate (CharacterStats.GetMeleeAttackBonus).
    /// </summary>
    private static int EstimatedMeleeModifier(CharacterController c, CharacterController target)
    {
        ItemData weapon = c.GetEquippedMainWeapon();
        if (weapon != null && weapon.IsWeapon && weapon.WeaponCat == WeaponCategory.Melee)
            return c.BuildAttackBonus(target, weapon, false, null, false, 0, c.Stats.BaseAttackBonus, CharacterController.IsWeaponTwoHanded(weapon)).Total
                   + weapon.GetEnhancementAttackBonus() + weapon.MasterworkAttackBonus;
        return c.Stats.GetMeleeAttackBonus();
    }

    /// <summary><paramref name="npc"/> and the living creatures of its side, and the reach of each living foe (TurnOptionValue.FoeReach).</summary>
    private void GatherSides(CharacterController npc, out List<CharacterController> side, out List<DND35.AI.Valuation.TurnOptionValue.FoeReach> foes)
    {
        side = new List<CharacterController>();
        foes = new List<DND35.AI.Valuation.TurnOptionValue.FoeReach>();
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (c == null || c.Stats == null || c.Stats.IsDead)
                continue;
            if (c == npc || TeamUtility.IsAlly(npc, c))
                side.Add(c);
            else if (_gameManager.IsEnemyTeamForAI(npc, c))
                foes.Add(new DND35.AI.Valuation.TurnOptionValue.FoeReach(c));
        }
    }

    /// <summary>
    /// Whether a living foe can attack <paramref name="npc"/> next round (TurnOptionValue.FoeReach.ReachOf: it threatens it,
    /// a ranged weapon in hand covers it, or one move or a charge brings it into reach). Total defense is taken only then (batch 14 step
    /// 13, AI-063): the melee turn's option and the out-of-reach list's entry (AiOutOfReach.Run).
    /// </summary>
    internal bool AnyFoeCanReach(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || _gameManager == null)
            return false;
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Action, npc);
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (c == null || c == npc || c.Stats == null || c.Stats.IsDead || !_gameManager.IsEnemyTeamForAI(npc, c))
                continue;
            if (new DND35.AI.Valuation.TurnOptionValue.FoeReach(c).ReachOf(npc) != DND35.AI.Valuation.TurnOptionValue.ReachKind.None)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The pure reads of AIProfile.ChooseMeleeTurnOption for <paramref name="npc"/> about to attack <paramref name="target"/>
    /// (batch 14 step 13, C8; the values of DND35.AI.Valuation.TurnOptionValue):
    /// <list type="bullet">
    /// <item>S: the full attack's expected damage on the target when the full-round action is free (AttackValue.ExpectedRoundDamage), else one attack's;</item>
    /// <item>aid offense and aid defense, when the aid-another entry is legal now: the best pair of a foe the creature
    /// threatens (it can make a melee attack on it) and an ally in melee with that foe (for aid offense, an ally that
    /// threatens it; for aid defense, either threatening the other, GameManager.IsEngagedInMelee), each checked by the
    /// entry's own target rule (GameManager.GetAidAnotherTargetRefusal, PHB p.154); the aid roll's modifier is the roll's own
    /// (GameManager.GetAidAnotherAttackModifier);</item>
    /// <item>total defense, when its entry is legal now: the foes' next-round damage it takes away (+4 AC), and whether any
    /// foe can reach the creature.</item>
    /// </list>
    /// Every foe hit in aid defense and total defense (the damage, the kept-off damage and the drop weight) is taken after
    /// the defending creature's resistance and damage reduction against that foe's attack (TurnOptionValue.AverageHitOn,
    /// FoeReach.DamageOn; AI-091); S and aid offense still read the raw hit (AI-093).
    /// Timed in the Action bucket; no dice, nothing changed.
    /// </summary>
    internal MeleeTurnOptionContext BuildMeleeTurnOptionContext(CharacterController npc, CharacterController target)
    {
        var ctx = new MeleeTurnOptionContext();
        if (npc == null || npc.Stats == null || npc.Actions == null || target == null || target.Stats == null || _gameManager == null)
            return ctx;
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Action, npc);
        GatherSides(npc, out List<CharacterController> side, out List<DND35.AI.Valuation.TurnOptionValue.FoeReach> foes);

        int targetAC = KnownAC(npc, target);
        ctx.AttackValue = npc.Actions.HasFullRoundAction
            ? DND35.AI.Valuation.AttackValue.ExpectedRoundDamage(npc, targetAC)
            : DND35.AI.Valuation.TurnOptionValue.SingleAttackDamage(npc, targetAC);

        if (AiLegal.CanUse(npc, ActionIds.AidAnother, _gameManager))
        {
            int aidModifier = GameManager.GetAidAnotherAttackModifier(npc);
            float bestOffense = 0f;
            float bestDefense = 0f;
            for (int f = 0; f < foes.Count; f++)
            {
                DND35.AI.Valuation.TurnOptionValue.FoeReach reach = foes[f];
                CharacterController opponent = reach.Foe;
                if (!DND35.AI.Knowledge.KnowledgeFacts.ThreatensSpaceOf(npc, opponent))
                    continue;
                int opponentAC = KnownAC(npc, opponent);
                for (int a = 0; a < side.Count; a++)
                {
                    CharacterController ally = side[a];
                    if (ally == npc)
                        continue;
                    // The ally attacks the opponent only where it threatens it; the opponent's next attack on the ally needs
                    // only the opponent to threaten it (the shared rule's "engaging", GameManager.IsEngagedInMelee).
                    bool allyThreatens = DND35.AI.Knowledge.KnowledgeFacts.ThreatensSpaceOf(ally, opponent);
                    if (!allyThreatens && !DND35.AI.Knowledge.KnowledgeFacts.ThreatensSpaceOf(opponent, ally))
                        continue;
                    if (allyThreatens && DND35.AI.Knowledge.KnowledgeFacts.CanAttackThisRound(ally))
                    {
                        float offense = DND35.AI.Valuation.TurnOptionValue.AidOffense(aidModifier, EstimatedMeleeModifier(ally, opponent), opponentAC,
                            DND35.AI.Valuation.AttackValue.AverageMeleeDamage(ally));
                        if (offense > bestOffense && _gameManager.GetAidAnotherTargetRefusal(npc, ally, opponent) == null)
                        {
                            bestOffense = offense;
                            ctx.CanAidOffense = true;
                            ctx.AidOffenseValue = offense;
                            ctx.AidOffenseAlly = ally;
                            ctx.AidOffenseOpponent = opponent;
                        }
                    }
                    float share = DND35.AI.Valuation.TurnOptionValue.Share(reach, ally, side);
                    if (share <= 0f)
                        continue;
                    // The opponent's hit on the ally, after the ally's resistance and damage reduction (AI-091).
                    float opponentHit = DND35.AI.Valuation.TurnOptionValue.AverageHitOn(opponent, ally);
                    float defense = DND35.AI.Valuation.TurnOptionValue.AidDefense(aidModifier, EstimatedMeleeModifier(opponent, ally), ally.Stats.ArmorClass,
                        opponentHit, share, DND35.AI.Valuation.TurnOptionValue.DropWeightFor(opponentHit, ally.Stats.CurrentHP));
                    if (defense > bestDefense && _gameManager.GetAidAnotherTargetRefusal(npc, ally, opponent) == null)
                    {
                        bestDefense = defense;
                        ctx.CanAidDefense = true;
                        ctx.AidDefenseValue = defense;
                        ctx.AidDefenseAlly = ally;
                        ctx.AidDefenseOpponent = opponent;
                    }
                }
            }
        }

        ctx.CanTotalDefense = AiLegal.CanUse(npc, ActionIds.TotalDefense, _gameManager);
        if (ctx.CanTotalDefense)
        {
            int ac = npc.Stats.ArmorClass;
            float keptOff = 0f, incoming = 0f;
            for (int f = 0; f < foes.Count; f++)
            {
                DND35.AI.Valuation.TurnOptionValue.FoeReach reach = foes[f];
                if (reach.ReachOf(npc) == DND35.AI.Valuation.TurnOptionValue.ReachKind.None)
                    continue;
                ctx.FoeCanReach = true;
                float share = DND35.AI.Valuation.TurnOptionValue.Share(reach, npc, side);
                if (share <= 0f)
                    continue;
                // The foe's hits after the creature's own resistance and damage reduction (AI-091): a foe that cannot pass
                // it neither drops the creature nor deals the damage total defense would keep off.
                float hit = DND35.AI.Valuation.TurnOptionValue.AverageHitOn(reach.Foe, npc);
                float atAC = reach.DamageOn(npc, ac);
                incoming += atAC * share;
                keptOff += DND35.AI.Valuation.TurnOptionValue.TotalDefensePart(atAC,
                    reach.DamageOn(npc, ac + DND35.AI.Valuation.TurnOptionValue.TotalDefenseBonus), share,
                    DND35.AI.Valuation.TurnOptionValue.DropWeightFor(hit, npc.Stats.CurrentHP));
            }
            ctx.TotalDefenseValue = DND35.AI.Valuation.TurnOptionValue.TotalDefense(keptOff, incoming, ctx.AttackValue);
        }
        return ctx;
    }

    /// <summary>
    /// The melee turn's option for <paramref name="npc"/> before it attacks <paramref name="target"/> (batch 14 step 13,
    /// AI-063, batch 13's C5; D3, D4, C8): the Role's AIProfile.ChooseMeleeTurnOption (the default without a profile),
    /// tilted by the traits (AiBlend.MeleeTurn). For an alternative, <paramref name="action"/> is its catalog context: the
    /// aid-another entry with its ally, opponent and variant, or the total-defense entry. One trace row "MeleeTurnOption"
    /// (legacy Attack, the turn before the step). Synchronous: the caller runs the chosen action.
    /// </summary>
    private MeleeTurnOption ChooseMeleeTurnOptionForAI(CharacterController npc, CharacterController target, AIProfile profile, out ActionContext action)
    {
        action = null;
        if (npc == null || npc.Stats == null || target == null || _gameManager == null || npc.IsGrappling())
            return MeleeTurnOption.Attack;
        MeleeTurnOptionContext ctx = BuildMeleeTurnOptionContext(npc, target);
        MeleeTurnOption roleChoice = profile != null ? profile.ChooseMeleeTurnOption(npc, ctx) : AIProfile.DefaultChooseMeleeTurnOption(ctx);
        MeleeTurnOption choice = DND35.AI.Personality.AiBlend.MeleeTurn(npc, roleChoice, ctx);
        if (!ctx.IsOpen(choice))
            choice = MeleeTurnOption.Attack;
        string values = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "S={0:0.##} aidOffense={1:0.##} aidDefense={2:0.##} totalDefense={3:0.##} margin={4:0.##} reach={5}",
            ctx.AttackValue, ctx.CanAidOffense ? ctx.AidOffenseValue : 0f, ctx.CanAidDefense ? ctx.AidDefenseValue : 0f,
            ctx.CanTotalDefense ? ctx.ValueOf(MeleeTurnOption.TotalDefense) : 0f, ctx.Margin, ctx.FoeCanReach);
        if (AiDecisionTrace.HasOpenTurn(npc))
            AiDecisionTrace.Record(npc, "MeleeTurnOption", choice.ToString(), MeleeTurnOption.Attack.ToString(),
                ctx.ValueOf(choice) - ctx.AttackValue, values + (choice != roleChoice ? " (Role " + roleChoice + ")" : ""));
        Debug.Log($"[AI][MeleeTurnOption] {npc.Stats.CharacterName}: {choice} (Role {roleChoice}; {values})");

        switch (choice)
        {
            case MeleeTurnOption.AidOffense:
            case MeleeTurnOption.AidDefense:
            {
                bool offense = choice == MeleeTurnOption.AidOffense;
                action = ActionContext.For(ActionIds.AidAnother, npc, ActorSource.AI, offense ? ctx.AidOffenseAlly : ctx.AidDefenseAlly);
                action.Opponent = offense ? ctx.AidOffenseOpponent : ctx.AidDefenseOpponent;
                action.Variant = offense ? AidAnotherActionExecutor.OffenseVariant : AidAnotherActionExecutor.DefenseVariant;
                return choice;
            }
            case MeleeTurnOption.TotalDefense:
                action = ActionContext.For(ActionIds.TotalDefense, npc, ActorSource.AI);
                return choice;
            default:
                return MeleeTurnOption.Attack;
        }
    }

    /// <summary>
    /// The melee routines' alternative to the attack (batch 14 step 13): the aid another or total defense
    /// <see cref="ChooseMeleeTurnOptionForAI"/> chose, run through the catalog (RunAIAction); once performed, the move left
    /// follows (UseMoveAfterStandardAction, D1). <paramref name="outcome"/>.Performed tells the routine its standard action
    /// was used; a refused alternative leaves the attack.
    /// </summary>
    private IEnumerator RunMeleeTurnAlternative(CharacterController npc, CharacterController target, ActionContext action, ActionOutcome outcome)
    {
        yield return _gameManager.StartCoroutine(_gameManager.RunAIAction(action, outcome));
        if (!outcome.Performed || CombatEnded() || npc.Stats == null || !npc.CanTakeTurnActions())
            yield break;
        yield return CombatPacing.Wait(0.5f);
        if (!KeepsRoutineAfterAbility(npc))
            yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, target, CombatStyle.Melee));
    }

    // ── Domain powers (batch 14 step 14, batch 13's C5 leftover; decision D7, design choices C8 and C14) ──

    /// <summary>
    /// Whether the domain-power entry would take <paramref name="domain"/> for <paramref name="npc"/> now (on
    /// <paramref name="target"/> for the touch): the turn's compulsion filter (AiLegal.CanUse) and the executor's own check
    /// (DomainPowerActionExecutor.CanExecute: the day's use, the standard action of the touch, a living target in natural
    /// reach). Pure.
    /// </summary>
    private bool DomainPowerLegal(CharacterController npc, string domain, CharacterController target = null)
    {
        if (npc.Stats.ChosenDomains == null || !npc.Stats.ChosenDomains.Contains(domain) || !AiLegal.CanUse(npc, ActionIds.DomainPower, _gameManager))
            return false;
        ActionDefinition def = ActionCatalog.Get(ActionIds.DomainPower);
        if (def == null || def.Executor == null)
            return false;
        ActionContext probe = ActionContext.For(ActionIds.DomainPower, npc, ActorSource.AI, target);
        probe.Variant = domain;
        return def.Executor.CanExecute(probe).Ok;
    }

    /// <summary>
    /// The turn-start reading of "it will make melee attacks this turn" (C14, Strength): only a grappling creature, whose
    /// grapple checks use Strength (PHB p.156) and which has no melee attack site of its own. Every other turn decides the
    /// feat of strength where its melee attack is certain (DomainPowerSite.MeleeAttack, Smite, ChargeAttack; batch 14 step
    /// 14 review), so a turn that aids another, defends, cures, retreats or casts first does not spend the day's use.
    /// </summary>
    private static bool WillMakeMeleeAttacksFromTurnStart(CharacterController npc)
        => npc != null && npc.IsGrappling();

    /// <summary>
    /// The pure reads of AIProfile.ChooseDomainPower for <paramref name="npc"/> at <paramref name="site"/> (batch 14 step
    /// 14, C8, C14; the values of DND35.AI.Valuation.DomainPowerValue), <paramref name="target"/> the routine's attack target:
    /// <list type="bullet">
    /// <item>TurnStart: the feat of strength, for a grappling creature only (WillMakeMeleeAttacksFromTurnStart);</item>
    /// <item>MeleeAttack: the feat of strength (it is about to attack in melee), and the death touch on the foe with the
    /// best value among those the domain-power entry would take (DomainPowerLegal: the executor's own check, a living
    /// creature past every direct-attack gate, the same for the PC's button), with S the attack it would replace, only
    /// while the standard action is left and no attack has been made. P(touch) is the core's formula (TargetNumbers'
    /// touch modifier and touch AC, the [KnowledgeLegacy] choke point) and the foe's current hit points are read true
    /// (TargetNumbers.CurrentHitPoints, until batch 17 hides them); an unrolled ward save and a death ward are not
    /// known, so such a foe is touched and the core rolls the save or finds the ward;</item>
    /// <item>Smite: the feat of strength and the smite, on the sequence's first swing with the target in the weapon's
    /// reach, the smite's gain from the swing's modifier (the shared attack terms), the known AC and the weapon's average
    /// hit;</item>
    /// <item>ChargeAttack (the AI's own charge, at its smite declaration): the feat of strength and the smite, as above.</item>
    /// </list>
    /// No dice, nothing changed; ChooseDomainPowerForAI times it.
    /// </summary>
    internal DomainPowerContext BuildDomainPowerContext(CharacterController npc, DomainPowerSite site, CharacterController target)
    {
        var ctx = new DomainPowerContext { Site = site };
        if (npc == null || npc.Stats == null || npc.Actions == null || npc.Stats.ChosenDomains == null || _gameManager == null)
            return ctx;
        bool targetInReach = target != null && target.Stats != null && !target.Stats.IsDead
                             && !npc.IsEquippedWeaponRanged() && npc.IsTargetInCurrentWeaponRange(target);
        int clericLevel = Mathf.Max(1, npc.Stats.GetClassLevel("Cleric"));

        switch (site)
        {
            case DomainPowerSite.TurnStart:
                ctx.StrengthOpen = DomainPowerLegal(npc, "Strength");
                ctx.WillMakeMeleeAttacks = ctx.StrengthOpen && WillMakeMeleeAttacksFromTurnStart(npc);
                break;

            case DomainPowerSite.MeleeAttack:
            {
                ctx.StrengthOpen = DomainPowerLegal(npc, "Strength");
                ctx.WillMakeMeleeAttacks = targetInReach;
                if (!targetInReach || !npc.Actions.HasStandardAction || npc.ProgressiveAttackPool.HasStartedAttacking
                    || !npc.Stats.ChosenDomains.Contains("Death"))
                    break;
                int targetAC = KnownAC(npc, target);
                ctx.AttackValue = npc.Actions.HasFullRoundAction
                    ? DND35.AI.Valuation.AttackValue.ExpectedRoundDamage(npc, targetAC)
                    : DND35.AI.Valuation.TurnOptionValue.SingleAttackDamage(npc, targetAC);
                List<CharacterController> all = _gameManager.GetAllCharactersForAI();
                for (int i = 0; i < all.Count; i++)
                {
                    CharacterController foe = all[i];
                    if (foe == null || foe == npc || foe.Stats == null || foe.Stats.IsDead || foe.Stats.CurrentHP <= 0 || !_gameManager.IsEnemyTeamForAI(npc, foe))
                        continue;
                    if (!DomainPowerLegal(npc, "Death", foe))
                        continue;
                    float touch = DND35.AI.Valuation.AttackValue.HitChance(
                        DND35.AI.Valuation.TargetNumbers.TouchAttackModifier(npc, foe, null),
                        DND35.AI.Valuation.TargetNumbers.TouchArmorClass(npc, foe));
                    float value = DND35.AI.Valuation.DomainPowerValue.DeathTouch(touch, clericLevel,
                        DND35.AI.Valuation.TargetNumbers.CurrentHitPoints(foe));
                    if (ctx.DeathTarget == null || value > ctx.DeathTouchValue)
                    {
                        ctx.DeathOpen = true;
                        ctx.DeathTouchValue = value;
                        ctx.DeathTarget = foe;
                    }
                }
                break;
            }

            case DomainPowerSite.Smite:
            case DomainPowerSite.ChargeAttack:
            {
                ctx.StrengthOpen = DomainPowerLegal(npc, "Strength");
                ctx.WillMakeMeleeAttacks = targetInReach;
                if (!targetInReach || npc.ProgressiveAttackPool.HasStartedAttacking || npc.Stats.DestructionSmiteActive
                    || !DomainPowerLegal(npc, "Destruction"))
                    break;
                ctx.DestructionOpen = true;
                ctx.SmiteGain = DND35.AI.Valuation.DomainPowerValue.SmiteGain(EstimatedMeleeModifier(npc, target), KnownAC(npc, target),
                    DND35.AI.Valuation.AttackValue.AverageMeleeDamage(npc), clericLevel);
                break;
            }
        }
        return ctx;
    }

    /// <summary>
    /// The domain power for <paramref name="npc"/> at <paramref name="site"/> (batch 14 step 14, D7, C14): the Role's
    /// AIProfile.ChooseDomainPower (the default without a profile) on <see cref="BuildDomainPowerContext"/>; no trait tilt
    /// (see the hook). For a power, <paramref name="action"/> is the domain-power entry with the domain as its Variant (and
    /// the touch's target). One trace row "DomainPower" when a power is open (legacy None: before the step no AI used one).
    /// The context and the choice are timed in the Action bucket (owner decision 2026-10-30), the trace text and the log
    /// outside it. Synchronous: GameManager.TryNPCDomainPowerForAI runs the action.
    /// </summary>
    internal DomainPowerChoice ChooseDomainPowerForAI(CharacterController npc, DomainPowerSite site, CharacterController target, out ActionContext action)
    {
        action = null;
        if (npc == null || npc.Stats == null || _gameManager == null)
            return DomainPowerChoice.None;
        DomainPowerContext ctx;
        DomainPowerChoice choice;
        using (AiDecisionTimer.Open(AiTimeBucket.Action, npc))
        {
            ctx = BuildDomainPowerContext(npc, site, target);
            if (!ctx.AnyOpen)
                return DomainPowerChoice.None;
            AIProfile profile = GetProfile(npc);
            choice = profile != null ? profile.ChooseDomainPower(npc, ctx) : AIProfile.DefaultChooseDomainPower(ctx);
            if (!ctx.IsOpen(choice))
                choice = DomainPowerChoice.None;
        }
        string values = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "site={0} strength={1}/{2} smite={3}:{4:0.##} death={5}:{6:0.##} on {7} S={8:0.##}",
            site, ctx.StrengthOpen, ctx.WillMakeMeleeAttacks, ctx.DestructionOpen, ctx.SmiteGain,
            ctx.DeathOpen, ctx.DeathTouchValue, TraceName(ctx.DeathTarget), ctx.AttackValue);
        float bonus = choice == DomainPowerChoice.Death ? ctx.DeathTouchValue - ctx.AttackValue
            : choice == DomainPowerChoice.Destruction ? ctx.SmiteGain : 0f;
        if (AiDecisionTrace.HasOpenTurn(npc))
            AiDecisionTrace.Record(npc, "DomainPower", choice.ToString(), DomainPowerChoice.None.ToString(), bonus, values);
        Debug.Log($"[AI][DomainPower] {npc.Stats.CharacterName}: {choice} ({values})");
        if (choice == DomainPowerChoice.None)
            return choice;

        action = ActionContext.For(ActionIds.DomainPower, npc, ActorSource.AI, choice == DomainPowerChoice.Death ? ctx.DeathTarget : null);
        action.Variant = choice.ToString(); // "Strength", "Destruction" or "Death": the domain's name
        return choice;
    }

    /// <summary>
    /// The prone creature's choice at its turn start (batch 14 step 13, D5, C8): the Role's AIProfile.ChooseProneStart (the
    /// default without a profile) on <see cref="BuildProneStartContext"/>. <paramref name="crawlSquare"/> is the crawl's
    /// square. One trace row "ProneStart" (legacy StandUp, the turn before the step).
    /// </summary>
    internal ProneStartChoice ChooseProneStartForAI(CharacterController npc, out Vector2Int? crawlSquare)
    {
        crawlSquare = null;
        if (npc == null || npc.Stats == null || _gameManager == null)
            return ProneStartChoice.StandUp;
        ProneStartContext ctx = BuildProneStartContext(npc);
        AIProfile profile = GetProfile(npc);
        ProneStartChoice choice = profile != null ? profile.ChooseProneStart(npc, ctx) : AIProfile.DefaultChooseProneStart(ctx);
        if (choice == ProneStartChoice.Crawl)
        {
            if (ctx.CrawlSquare.HasValue && ctx.CanCrawl)
                crawlSquare = ctx.CrawlSquare;
            else
                choice = ProneStartChoice.StayProne;
        }
        string reason = $"threatened={ctx.Threatened} worksProne={ctx.PlannedActionWorksProne} rangedFoe={ctx.RangedFoeInView} reachable={ctx.SquareReachableNextTurn} crawl={(ctx.CrawlSquare.HasValue ? ctx.CrawlSquare.Value.ToString() : "none")}";
        if (AiDecisionTrace.HasOpenTurn(npc))
            AiDecisionTrace.Record(npc, "ProneStart", choice.ToString(), ProneStartChoice.StandUp.ToString(), 0f, reason);
        Debug.Log($"[AI][ProneStart] {npc.Stats.CharacterName}: {choice} ({reason})");
        return choice;
    }

    /// <summary>
    /// The pure reads of AIProfile.ChooseProneStart for the prone <paramref name="npc"/> (batch 14 step 13, D5, C8): whether
    /// a foe threatens any square of its space; whether a foe in view fights with a ranged weapon (the WieldsRanged fact);
    /// whether its planned action works prone (<see cref="PlannedActionWorksProne"/>); and, when all hold, whether a foe
    /// could threaten its space next turn (after a move or a charge) and the adjacent square a crawl reaches (the crawl
    /// entry's own destination rule) whose whole shifted space no foe threatens now or could next turn, farthest from the
    /// nearest foe. A read that cannot change the default answer is
    /// skipped. Timed in the Action bucket.
    /// </summary>
    internal ProneStartContext BuildProneStartContext(CharacterController npc)
    {
        var ctx = new ProneStartContext();
        if (npc == null || npc.Stats == null || _gameManager == null)
            return ctx;
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Action, npc);
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        // Every square of its space: a Large creature threatened on any of them is threatened (PHB p.137).
        List<Vector2Int> mine = npc.GetOccupiedSquares();
        for (int s = 0; s < mine.Count && !ctx.Threatened; s++)
            ctx.Threatened = ThreatSystem.IsSquareThreatened(mine[s], npc, all);
        if (ctx.Threatened)
            return ctx;

        GatherSides(npc, out _, out List<DND35.AI.Valuation.TurnOptionValue.FoeReach> foes);
        CharacterController nearest = null;
        int nearestDistance = int.MaxValue;
        for (int i = 0; i < foes.Count; i++)
        {
            CharacterController foe = foes[i].Foe;
            int d = npc.GetMinimumDistanceToTarget(foe, chebyshev: false);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearest = foe;
            }
            if (!ctx.RangedFoeInView && foes[i].CanAttack)
            {
                DND35.AI.Knowledge.ITargetView view = DND35.AI.Knowledge.KnowledgeService.ViewOf(npc, foe);
                DND35.AI.Knowledge.FactValue ranged = view != null ? view.Get(DND35.AI.Knowledge.FactKey.WieldsRanged) : DND35.AI.Knowledge.FactValue.Unknown;
                ctx.RangedFoeInView = ranged.IsKnown && ranged.Flag;
            }
        }
        if (!ctx.RangedFoeInView)
            return ctx;
        ctx.PlannedActionWorksProne = PlannedActionWorksProne(npc, nearest);
        if (!ctx.PlannedActionWorksProne)
            return ctx;

        for (int i = 0; i < foes.Count && !ctx.SquareReachableNextTurn; i++)
            for (int s = 0; s < mine.Count && !ctx.SquareReachableNextTurn; s++)
                ctx.SquareReachableNextTurn = foes[i].CouldThreatenNextTurn(mine[s]);
        ctx.CanCrawl = AiLegal.CanUse(npc, ActionIds.Crawl, _gameManager);
        if (!ctx.SquareReachableNextTurn || !ctx.CanCrawl)
            return ctx;

        int bestDistance = int.MinValue;
        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(npc.GridPosition);
        for (int n = 0; n < neighbors.Length; n++)
        {
            Vector2Int cell = neighbors[n];
            if (!_gameManager.IsValidCrawlDestinationForActions(npc, cell))
                continue;
            // The whole space after the crawl, each occupied square shifted by the step: no foe threatens any of it now (the
            // crawl provokes from a foe threatening the space it leaves or enters, PHB p.142) or could next turn.
            Vector2Int delta = cell - npc.GridPosition;
            bool safe = true;
            for (int i = 0; i < foes.Count && safe; i++)
                for (int s = 0; s < mine.Count && safe; s++)
                    safe = !foes[i].CouldThreatenNextTurn(mine[s] + delta);
            if (!safe)
                continue;
            int d = nearest != null ? SquareGridUtils.GetDistance(cell, nearest.GridPosition) : 0;
            if (d > bestDistance)
            {
                bestDistance = d;
                ctx.CrawlSquare = cell;
            }
        }
        return ctx;
    }

    /// <summary>
    /// Whether <paramref name="npc"/>'s turn plan works prone (PHB p.311: no ranged weapon but a crossbow; casting is not
    /// barred), read from what the turn will do rather than the Role alone (batch 14 step 13 review):
    /// <list type="bullet">
    /// <item>a Ranged Role whose main weapon is a crossbow it can shoot now: true;</item>
    /// <item>a Melee Role: false (its routine must move or 5-foot step, which a prone creature cannot, MovementService);</item>
    /// <item>a Healer Role: only on an OffensiveSpell turn (HealerAIProfile.DetermineActionPriority): a Healing turn's cure
    /// is a touch that may need step 12's approach, and a PhysicalAttack turn fights with its weapon;</item>
    /// <item>a casting Role: its turn's best spell (SelectSpell, the ScoreCastableSpells pick) reaches <paramref name="focus"/>
    /// from where it lies, and nothing in its main hand would send a turn whose spells find no target to a shot or a throw
    /// a prone attacker cannot make: a ranged weapon other than a crossbow, or a throwable melee weapon the kiter would
    /// throw (AiOutOfReach.ApproachRangeSquares). PHB p.311's bar is not enforced on the attack itself yet (CMB-196), so
    /// this keeps the AI from shooting prone.</item>
    /// </list>
    /// No profile: false (it stands, as before).
    /// </summary>
    private bool PlannedActionWorksProne(CharacterController npc, CharacterController focus)
    {
        AIProfile profile = GetProfile(npc);
        if (profile == null)
            return false;
        ItemData weapon = npc.GetEquippedMainWeapon();
        if (profile.CombatStyle == CombatStyle.Ranged && weapon != null && weapon.IsCrossbowWeapon && AIProfile.HasUsableRangedWeapon(npc))
            return true;
        if (!profile.IsCastingProfile || profile.CombatStyle == CombatStyle.Melee)
            return false;
        if (ConfusedBehaviorController.ProneBarsMainWeapon(npc)
            || (weapon != null && weapon.RangeIncrement > 0 && GameManager.IsThrowableWeapon(weapon, meleeOnly: true)))
            return false;
        if (profile is HealerAIProfile healer)
        {
            bool hasCastableSpells = HasCastablePreparedSpells(npc);
            bool hasCastableCure = hasCastableSpells && HealerAIProfile.HasCastableCure(npc, _gameManager);
            if (healer.DetermineActionPriority(npc, _gameManager.GetAllCharactersForAI(), hasCastableSpells, hasCastableCure) != HealerActionType.OffensiveSpell)
                return false;
        }
        if (focus == null || !HasCastableRangedSpell(npc, focus))
            return false;
        return GetBestAvailableSpellRangeInSquares(npc, focus) >= npc.GetMinimumDistanceToTarget(focus, chebyshev: false);
    }

    // ── The healer reaches its patient (batch 14 step 12, AI-047, AI-063; decision D1, design choice C8, owner question Q8) ──

    /// <summary>
    /// A spell that cures hit points (SpellDiceRules.CuresHitPoints: the cure ... wounds spells and Heal, PHB p.32), the
    /// filter of the healer's first spell walk and of its cast after the approach (batch 14 step 12, AI-063).
    /// </summary>
    private static bool IsHealingEffectSpell(SpellData spell) => SpellDiceRules.CuresHitPoints(spell);

    /// <summary>What <see cref="RunHealerApproach"/> did: whether it ended the healer's turn, or moved and left the standard action.</summary>
    private sealed class HealerApproachOutcome
    {
        public bool TurnDone;
        public bool Moved;
    }

    /// <summary>
    /// The pure reads of AIProfile.ChooseHealerApproach for <paramref name="npc"/> and its <paramref name="patient"/>
    /// (batch 14 step 12, C8):
    /// - the price per provoker of a path that provokes (Q8, owner default 2026-10-30): the Role's (1000 when it avoids
    ///   AoOs, as the Healer does; else 2), at most 6 (AiBlend.AoOAgainstFloor) when the patient is at or below the critical
    ///   threshold (HealerAIProfile.IsCriticallyWounded, 25%), then the traits' ProvokeAoO vote (AiBlend.AoOCost: a Forbid
    ///   is 1000 again, so a Cowardly healer that is hurt never provokes);
    /// - the provokers a touch path may have (<see cref="MaxTouchPathProvokers"/>, delegated reading of Q8, 2026-11-23):
    ///   the cure now is worth 6, so a path whose provokers times the price exceed 6 is refused;
    /// - a square beside the patient (a touch spell's reach, PHB p.175) that a 5-foot step reaches (PHB p.144: no move
    ///   this round, it provokes nothing), else that one move action reaches (GameManager.FindTouchApproachSquareForAI,
    ///   each provoker priced as that many squares). Casting there provokes from every foe that threatens it (PHB p.141)
    ///   unless the cure is cast defensively or quickened, and the AI declines a threatened cast it would not make
    ///   defensively (AISpellcastingStrategist.EvaluateDefensiveCasting): so when no castable cure would be cast
    ///   defensively (<see cref="CanCastCureWhileThreatened"/>) a threatened square is refused, else an unthreatened one
    ///   comes first;
    /// - else a move's destination closer to the patient (EvaluateMovementOptions toward a range of 1). That move touches
    ///   no one this turn, so it keeps the Healer's own price, not the critical one: no provoking destination when that
    ///   price forbids provoking.
    /// The healer's own footprint and reach are not read yet (a Large healer is measured from its anchor square; AI-047).
    /// </summary>
    internal HealerApproachContext BuildHealerApproachContext(CharacterController npc, HealerAIProfile healer, CharacterController patient)
    {
        var ctx = new HealerApproachContext();
        if (npc == null || npc.Stats == null || npc.Actions == null || _gameManager == null || _gameManager.Grid == null
            || patient == null || patient == npc || patient.Stats == null || patient.Stats.IsDead)
            return ctx;
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Movement, npc);
        ctx.HasPatient = true;
        ctx.Distance = CasterLevelRules.DistanceToTarget(npc, patient);
        ctx.PatientCritical = healer != null && healer.IsCriticallyWounded(patient);
        AIProfile profile = GetProfile(npc);
        float rolePrice = AvoidsAoOs(npc, profile) ? DND35.AI.Personality.AiBlend.AoOForbidPrice : DND35.AI.Personality.AiBlend.AoOForCeiling;
        float touchRolePrice = ctx.PatientCritical ? Mathf.Min(rolePrice, DND35.AI.Personality.AiBlend.AoOAgainstFloor) : rolePrice;
        ctx.AoOPrice = DND35.AI.Personality.AiBlend.AoOCost(npc, touchRolePrice);
        if (ctx.Distance <= 1)
            return ctx; // in reach already: the cure's failure is not the distance

        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        bool castsWhenThreatened = CanCastCureWhileThreatened(npc);
        Func<Vector2Int, bool> threatened = c => ThreatSystem.IsSquareThreatened(c, npc, all);

        // A 5-foot step beside the patient.
        if (_gameManager.CanTakeFiveFootStepForAI(npc))
        {
            Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(npc.GridPosition);
            bool bestSafe = false;
            for (int i = 0; i < neighbors.Length; i++)
            {
                Vector2Int c = neighbors[i];
                if (TouchReachFrom(c, patient) > 1 || !_gameManager.CanTakeFiveFootStepToForAI(npc, c))
                    continue;
                bool safe = !threatened(c);
                if (!safe && !castsWhenThreatened)
                    continue;
                if (!ctx.TouchSquare.HasValue || (safe && !bestSafe))
                {
                    ctx.TouchSquare = c;
                    ctx.TouchByFiveFootStep = true;
                    bestSafe = safe;
                }
            }
            if (ctx.TouchSquare.HasValue)
                return ctx;
        }

        if (!AiLegal.CanMoveNow(npc, _gameManager) || npc.Stats.MovementBlockedByCondition)
            return ctx;
        int maxProvokers = MaxTouchPathProvokers(ctx.AoOPrice);
        float pricePerProvoker = maxProvokers == 0 ? 0f : ctx.AoOPrice;
        ctx.TouchSquare = _gameManager.FindTouchApproachSquareForAI(npc, patient, maxProvokers, pricePerProvoker,
            rejectSquare: castsWhenThreatened ? null : threatened,
            preferSquare: castsWhenThreatened ? (Func<Vector2Int, bool>)(c => !threatened(c)) : null);
        if (ctx.TouchSquare.HasValue)
            return ctx;

        // The partial approach touches no one this turn: the Healer's own price (the Role's, priced by the traits).
        float partialPrice = ctx.PatientCritical ? DND35.AI.Personality.AiBlend.AoOCost(npc, rolePrice) : ctx.AoOPrice;
        bool partialNeverProvokes = partialPrice >= DND35.AI.Personality.AiBlend.AoOForbidPrice;
        SquareCell cell = EvaluateMovementOptions(npc, patient.GridPosition, retreat: false, targetCharacter: null, profile: profile,
            preferredRangeOverride: 1, rejectProvokingPaths: partialNeverProvokes);
        if (cell != null && cell.Coords != npc.GridPosition
            && TouchReachFrom(cell.Coords, patient) < TouchReachFrom(npc.GridPosition, patient))
            ctx.PartialSquare = cell.Coords;
        return ctx;
    }

    /// <summary>
    /// The provokers a healer's touch path may have at <paramref name="pricePerProvoker"/> (batch 14 step 12, delegated
    /// reading of Q8, 2026-11-23): the cure now is worth AiBlend.AoOAgainstFloor (6), so provokers times the price may not
    /// exceed it: one provoker at the critical price 6, three at 2, none at 1000 or more, any at a price of 0 or less.
    /// </summary>
    internal static int MaxTouchPathProvokers(float pricePerProvoker)
    {
        if (pricePerProvoker <= 0f)
            return int.MaxValue;
        if (pricePerProvoker >= DND35.AI.Personality.AiBlend.AoOForbidPrice)
            return 0;
        return Mathf.FloorToInt(DND35.AI.Personality.AiBlend.AoOAgainstFloor / pricePerProvoker + 0.0001f);
    }

    /// <summary>
    /// Whether <paramref name="npc"/> would cast one of its castable cures (HealerAIProfile.CastableCures) in a threatened
    /// square without provoking (batch 14 step 12): a quickened cure (PHB p.98; CastActionRules.IsQuickened) or one the AI
    /// casts defensively (AISpellcastingStrategist.ShouldCastDefensively, PHB p.140), as EvaluateDefensiveCasting decides.
    /// </summary>
    private bool CanCastCureWhileThreatened(CharacterController npc)
    {
        List<SpellData> cures = HealerAIProfile.CastableCures(npc, _gameManager);
        for (int i = 0; i < cures.Count; i++)
        {
            SpellData cure = cures[i];
            if (CastActionRules.IsQuickened(SpellCastRequest.AiMetamagicFor(cure), SpellCastRequest.AiSourceFor(npc, cure))
                || AISpellcastingStrategist.ShouldCastDefensively(npc, cure))
                return true;
        }
        return false;
    }

    /// <summary>The distance in squares from <paramref name="cell"/> to the nearest square <paramref name="patient"/> occupies (1 is a touch's reach).</summary>
    private static int TouchReachFrom(Vector2Int cell, CharacterController patient)
    {
        List<Vector2Int> squares = patient.GetOccupiedSquares();
        if (squares == null || squares.Count == 0)
            return SquareGridUtils.GetDistance(cell, patient.GridPosition);
        int best = int.MaxValue;
        for (int i = 0; i < squares.Count; i++)
            best = Mathf.Min(best, SquareGridUtils.GetDistance(cell, squares[i]));
        return best;
    }

    /// <summary>
    /// The healer's approach after its cure found no patient in reach (batch 14 step 12, AI-047, AI-063; C8): the Role's
    /// AIProfile.ChooseHealerApproach on <see cref="BuildHealerApproachContext"/>, tilted by the traits
    /// (AiBlend.HealerApproach), one decision-trace row "HealerApproach". MoveAndCast takes the 5-foot step (through
    /// GameManager.TryTakeFiveFootStepForAI) or the move (the catalog's move entry, RunAIAction) beside the patient, then
    /// casts a cure in the same turn (PHB p.138, D1), which ends the turn; when the cure is then refused the kiter uses
    /// the standard action. PartialApproach moves toward the patient and leaves the standard action to the kiter. Kiter
    /// does nothing here: the caller runs today's turn.
    /// </summary>
    private IEnumerator RunHealerApproach(CharacterController npc, HealerAIProfile healer, CharacterController patient, HealerApproachOutcome outcome)
    {
        if (npc == null || npc.Stats == null || outcome == null)
            yield break;
        HealerApproachContext ctx = BuildHealerApproachContext(npc, healer, patient);
        AIProfile profile = GetProfile(npc);
        HealerApproachChoice roleChoice = profile != null
            ? profile.ChooseHealerApproach(npc, ctx)
            : AIProfile.DefaultChooseHealerApproach(ctx);
        HealerApproachChoice choice = DND35.AI.Personality.AiBlend.HealerApproach(npc, roleChoice, ctx);
        string name = npc.Stats.CharacterName;
        string patientName = patient != null && patient.Stats != null ? patient.Stats.CharacterName : "?";
        if (AiDecisionTrace.HasOpenTurn(npc))
            AiDecisionTrace.Record(npc, "HealerApproach", choice.ToString(), roleChoice.ToString(), 0f,
                ctx.HasPatient
                    ? $"distance={ctx.Distance} critical={ctx.PatientCritical} aooPrice={ctx.AoOPrice:0.##} touch={(ctx.TouchSquare.HasValue ? (ctx.TouchByFiveFootStep ? "step" : "move") : "none")} partial={ctx.PartialSquare.HasValue}"
                    : "no patient");
        Debug.Log($"[AI][Healer] {name}: approach {choice} (Role {roleChoice}) to {patientName}; distance={ctx.Distance} critical={ctx.PatientCritical} aooPrice={ctx.AoOPrice:0.##} touch={ctx.TouchSquare} step={ctx.TouchByFiveFootStep} partial={ctx.PartialSquare}");

        switch (choice)
        {
            case HealerApproachChoice.MoveAndCast:
            {
                if (!ctx.TouchSquare.HasValue)
                    yield break;
                if (ctx.TouchByFiveFootStep)
                {
                    if (!_gameManager.TryTakeFiveFootStepForAI(npc, ctx.TouchSquare.Value))
                        yield break;
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} takes a 5-foot step to reach {patientName}."));
                    yield return CombatPacing.Wait(0.35f);
                }
                else
                {
                    ActionContext moveCtx = ActionContext.For(ActionIds.Move, npc, ActorSource.AI);
                    moveCtx.Cell = ctx.TouchSquare.Value;
                    var move = new ActionOutcome();
                    yield return _gameManager.StartCoroutine(_gameManager.RunAIAction(moveCtx, move));
                    if (CombatEnded() || npc.Stats == null || !npc.CanTakeTurnActions())
                    {
                        outcome.TurnDone = true;
                        yield break;
                    }
                    if (!move.Performed)
                        yield break; // refused, nothing spent: today's turn
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} moves to reach {patientName}."));
                    yield return CombatPacing.Wait(0.5f);
                }
                outcome.Moved = true;
                if (CombatEnded() || npc.Stats == null || !npc.CanTakeTurnActions())
                {
                    outcome.TurnDone = true;
                    yield break;
                }
                if (TryExecuteSpellcastAction(npc, patient, IsHealingEffectSpell))
                {
                    Debug.Log($"[AI][Healer] {name} moved and healed {patientName}.");
                    yield return CombatPacing.Wait(0.5f);
                    // A quickened cure keeps the standard action (PHB p.98): the kiter uses it.
                    outcome.TurnDone = !KeepsRoutineAfterAbility(npc);
                }
                yield break;
            }

            case HealerApproachChoice.PartialApproach:
            {
                if (!ctx.PartialSquare.HasValue || !AiLegal.CanMoveNow(npc, _gameManager))
                    yield break;
                ActionContext moveCtx = ActionContext.For(ActionIds.Move, npc, ActorSource.AI);
                moveCtx.Cell = ctx.PartialSquare.Value;
                var move = new ActionOutcome();
                yield return _gameManager.StartCoroutine(_gameManager.RunAIAction(moveCtx, move));
                if (CombatEnded() || npc.Stats == null || !npc.CanTakeTurnActions())
                {
                    outcome.TurnDone = true;
                    yield break;
                }
                if (!move.Performed)
                    yield break;
                outcome.Moved = true;
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} moves toward {patientName} to heal them."));
                yield return CombatPacing.Wait(0.5f);
                yield break;
            }
        }
    }

    /// <summary>The Role's legacy AoO avoidance (Movement.AvoidAoOs, unless the profile ignores AoOs now), as EvaluateMovementOptions reads it.</summary>
    private static bool AvoidsAoOs(CharacterController npc, AIProfile profile)
        => profile != null && profile.Movement != null && profile.Movement.AvoidAoOs && !profile.ShouldIgnoreAoO(npc);

    /// <summary>The routine style of a creature with no profile on the top-level sites (lay on hands, turn undead, bardic music, a maintained spell).</summary>
    private static CombatStyle StyleWithoutProfile(NPCAIBehavior behavior)
        => behavior == NPCAIBehavior.RangedKiter || behavior == NPCAIBehavior.Ranged ? CombatStyle.Ranged : CombatStyle.Melee;

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
                int distance = npc.GetMinimumDistanceToTarget(target, chebyshev: false);
                int rangeSquares = preMovAttack.RangeFeet / 5;
                
                if (distance <= rangeSquares)
                {
                    Debug.Log($"[AI] {npc.Stats.CharacterName} attempting ranged special attack (distance {distance} squares, range {rangeSquares} squares)");
                    if (TryExecuteRangedSpecialAttack(npc, target)) // the core ran the combat-end check
                    {
                        yield return CombatPacing.Wait(0.8f);
                        yield break;
                    }
                }
            }
        }

        // A melee caster casts first (batch 13 step 8, AI-002): the turn's spell through the catalog's cast entry, when
        // the profile asks for it (AIProfile.ShouldTryCastBeforeMelee: a castable spell with a combat effect). The move
        // action follows the cast (batch 14 step 11, AI-077: AIProfile.ChooseAfterStandardAction, a melee Role closing on
        // its target); a quickened cast keeps the melee turn going (PHB p.98); a held touch charge is delivered by
        // ExecuteNPCTurn. With no spell worth casting nothing is spent and the melee turn goes on.
        if (TryCastBeforeMelee(npc, target, profile))
        {
            yield return CombatPacing.Wait(0.8f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, target, CombatStyle.Melee));
                yield break;
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
        if (!npc.IsTargetInCurrentWeaponRange(target) && AiLegal.CanMoveNow(npc, _gameManager))
        {
            triedApproach = true;
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                var advanceMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, advanceMove)));

                // ── Death/disable check after movement ──
                // Creature may have been killed by area damage (Wall of Fire, etc.) during movement.
                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dropped during movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!advanceMove.Refused) // a refused move spends nothing and the routine goes on (D3; the executor spent this one first)
                {
                    // Activate terrain manipulation after moving closer
                    if (npc.HasTerrainManipulation)
                        ActivateTerrainManipulation(npc);

                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!"));
                    yield return CombatPacing.Wait(0.5f);
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
            if (npc.HasEngulf && EngulfActionExecutor.IsAdjacent(npc, target))
            {
                Debug.Log($"[AI] {npc.Stats.CharacterName} attempting engulf");
                if (TryExecuteEngulf(npc, target))
                {
                    yield return CombatPacing.Wait(0.8f);
                    yield break;
                }
            }

            // The standard action's alternatives to the attack (batch 14 step 13, AI-063, C8): aid another (PHB p.154, D3)
            // or total defense (PHB p.142, D4) where its value beats the attack's by the margin; the move left follows.
            if (ChooseMeleeTurnOptionForAI(npc, target, profile, out ActionContext alternative) != MeleeTurnOption.Attack)
            {
                var alternativeOutcome = new ActionOutcome();
                yield return _gameManager.StartCoroutine(RunMeleeTurnAlternative(npc, target, alternative, alternativeOutcome));
                if (alternativeOutcome.Performed || CombatEnded() || !npc.CanTakeTurnActions())
                    yield break;
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
                    int postMoveDistance = npc.GetMinimumDistanceToTarget(target, chebyshev: false);
                    int rangeSquares = postMovAttack.RangeFeet / 5;
                    if (postMoveDistance <= rangeSquares)
                    {
                        Debug.Log($"[AI] {npc.Stats.CharacterName} attempting post-movement ranged special attack (distance {postMoveDistance} squares, range {rangeSquares} squares)");
                        if (TryExecuteRangedSpecialAttack(npc, target)) // the core ran the combat-end check
                        {
                            yield return CombatPacing.Wait(0.8f);
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
        // Through the catalog's maintain-concentration entry (batch 13 step 6).
        return maintain && _gameManager.RunAIActionNow(ActionContext.For(ActionIds.MaintainConcentration, npc, ActorSource.AI)).Performed;
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
                yield return CombatPacing.Wait(0.8f);
                // The move action after the cast (batch 14 step 11, AI-077); a quickened cast keeps the routine going.
                if (!KeepsRoutineAfterAbility(npc))
                {
                    yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, target, CombatStyle.Melee));
                    yield break;
                }
            }
        }

        // ── Priority 5: Tactical Breath Weapon Positioning ──
        // When breath weapon is available, evaluate ALL reachable positions to find
        // the optimal spot that maximizes enemy hits. Move there, then fire. The breath entry's start gate (ready, an
        // actor free to act, and a standard action to spend: MM p.69, p.315; batch 13 step 18, D11, AI-040) decides
        // whether to try at all.
        ActionDefinition breathEntry = ActionCatalog.Get(ActionIds.BreathWeapon);
        if (npc.HasBreathWeapon && npc.IsBreathWeaponReady
            && (breathEntry == null || breathEntry.Executor == null || breathEntry.Executor.CanStart(npc).Ok))
        {
            Debug.Log($"🐉 [AI][Dragon] {npc.Stats.CharacterName} — breath weapon READY, initiating tactical positioning...");

            var bestBreathPos = dragonProfile.FindBestBreathPosition(npc, allCombatants, _gameManager);

            if (bestBreathPos.HasValue)
            {
                var bp = bestBreathPos.Value;

                // ── Step 1: Move to optimal position if needed ──
                // Only when the standard action is left after the move: a creature limited to a single action (disabled,
                // staggered, slowed; ActionEconomy.SingleActionOnly) would spend its turn on the move and could not
                // breathe (batch 13 step 18 review), so it weighs the breath from where it stands.
                if (bp.RequiresMove && AiLegal.CanMoveNow(npc, _gameManager) && !npc.Actions.SingleActionOnly)
                {
                    Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName} moving from {npc.GridPosition} → {bp.Position} " +
                              $"for breath weapon ({bp.EnemyHits} targets)");

                    var breathMove = new MoveAttempt();
                    yield return _gameManager.StartCoroutine(
                        _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, bp.Position, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, breathMove)));

                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                    if (!npc.CanTakeTurnActions())
                    {
                        Debug.Log($"🔥 [AI][Dragon] {npc.Stats.CharacterName} killed during breath positioning move — turn ended");
                        yield break;
                    }
                    if (breathMove.Refused)
                    {
                        // Refused before the first step (no path, or the shared legality): nothing spent (D3); the breath
                        // is weighed again from where it stands, as with no move action.
                        Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName} cannot reach the breath position ({breathMove.Reason}) — using current position");
                    }
                    else
                    {
                        // The executor spent the move action before the first step.
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐉",
                            $"{npc.Stats.CharacterName} maneuvers into optimal breath weapon position!"));
                        yield return CombatPacing.Wait(0.4f);
                    }
                }
                else if (bp.RequiresMove)
                {
                    // Can't move and still breathe — fall back to evaluating breath from current position only
                    Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName} wants to reposition but has no move action to spare — using current position");
                }

                // ── Step 2: Fire breath weapon ──
                // Re-evaluate from actual current position (may have moved or stayed)
                bool breathReady = dragonProfile.EvaluateBreathWeapon(npc, allCombatants, _gameManager);
                if (breathReady && dragonProfile.BreathWeaponAimTarget != null)
                {
                    Debug.Log($"🐉 [AI][Dragon] {npc.Stats.CharacterName} FIRES breath weapon from {npc.GridPosition} " +
                              $"aimed at {dragonProfile.BreathWeaponAimTarget.Stats.CharacterName} " +
                              $"(hits {dragonProfile.BreathWeaponExpectedHits} enemies)!");

                    var breathOutcome = new ActionOutcome();
                    yield return _gameManager.StartCoroutine(
                        _gameManager.NPCExecuteBreathWeaponForAI(npc, dragonProfile.BreathWeaponAimTarget, breathOutcome));
                    if (CombatEnded()) yield break; // the breath ran the combat-end check
                    // A refused breath spends nothing: the turn goes on to the melee priorities (batch 13 step 18 review).
                    bool breathed = breathOutcome.Performed;
                    if (!breathed)
                        Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName}'s breath was refused ({breathOutcome.Refusal.Reason}) — proceeding to melee");

                    // After breath weapon, dragon may still have move action for repositioning
                    if (breathed && npc.CanTakeTurnActions() && npc.Actions.HasMoveAction)
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
                                        _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, repositionMove)));
                                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                                    if (!npc.CanTakeTurnActions() || repositionMove.Refused)
                                        yield break; // dropped while moving (CMB-073), or refused before the first step: nothing spent (D3)
                                    // The executor spent the move action before the first step.
                                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐉",
                                        $"{npc.Stats.CharacterName} repositions after breath weapon."));
                                    yield return CombatPacing.Wait(0.3f);
                                }
                            }
                        }
                    }

                    if (breathed)
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
                    var fallbackBreathOutcome = new ActionOutcome();
                    yield return _gameManager.StartCoroutine(
                        _gameManager.NPCExecuteBreathWeaponForAI(npc, dragonProfile.BreathWeaponAimTarget, fallbackBreathOutcome));
                    if (CombatEnded()) yield break; // the breath ran the combat-end check
                    bool fallbackBreathed = fallbackBreathOutcome.Performed;
                    if (!fallbackBreathed)
                        Debug.Log($"🐉 [AI][DragonBreath] {npc.Stats.CharacterName}'s breath was refused ({fallbackBreathOutcome.Refusal.Reason}) — proceeding to melee");

                    if (fallbackBreathed && npc.CanTakeTurnActions() && npc.Actions.HasMoveAction)
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
                                        _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, repositionMove)));
                                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                                    if (!npc.CanTakeTurnActions() || repositionMove.Refused)
                                        yield break; // dropped while moving (CMB-073), or refused before the first step: nothing spent (D3)
                                    // The executor spent the move action before the first step.
                                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐉",
                                        $"{npc.Stats.CharacterName} repositions after breath weapon."));
                                    yield return CombatPacing.Wait(0.3f);
                                }
                            }
                        }
                    }

                    if (fallbackBreathed)
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
        if (!npc.IsTargetInCurrentWeaponRange(target) && AiLegal.CanMoveNow(npc, _gameManager))
        {
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, dragonProfile);
            if (bestCell != null)
            {
                var advanceMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, advanceMove)));

                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI][Dragon] {npc.Stats.CharacterName} killed during movement — turn ended");
                    yield break;
                }

                if (!advanceMove.Refused) // a refused move spends nothing and the routine goes on (D3; the executor spent this one first)
                {
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!"));
                    yield return CombatPacing.Wait(0.5f);
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
            // And only with an attack left to declare (AiLegal.CanAttackNow; batch 14 step 10, AI-080).
            bool blindAttackAllowed = AiLegal.CanAttackNow(npc, _gameManager) && AiAttackGuard.Passes(npc, closestPC);
            if (blindAttackAllowed && tracker.IsPinpointedThisRound(closestPC))
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} pinpoints {closestPC.Stats.CharacterName} by sound and attacks their current position."));
            else if (blindAttackAllowed && lastKnown.HasValue)
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{npc.Stats.CharacterName} cannot see {closestPC.Stats.CharacterName} clearly and fires at the last known position."));

            if (blindAttackAllowed && (tracker.IsPinpointedThisRound(closestPC) || lastKnown.HasValue))
            {
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, closestPC));
                yield return CombatPacing.Wait(0.45f);
                yield break;
            }

            if (npc.Actions.HasMoveAction && MovementService.CanStartMovement(npc, MoveKind.Move).Ok)
            {
                SquareCell blindSearchCell = EvaluateMovementOptions(npc, closestPC.GridPosition, retreat: false, closestPC, profile);
                if (blindSearchCell != null && blindSearchCell.Coords != npc.GridPosition)
                {
                    var blindMove = new MoveAttempt();
                    yield return _gameManager.StartCoroutine(
                        _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, blindSearchCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, blindMove)));
                    if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                    if (!npc.CanTakeTurnActions() || blindMove.Refused)
                        yield break; // dropped while moving (CMB-073), or refused before the first step: nothing spent (D3)
                    // The executor spent the move action before the first step.
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances, trying to reacquire line of sight through concealment."));
                    yield return CombatPacing.Wait(0.4f);
                }
            }

            yield break;
        }

        // The move action after the cast (batch 14 step 11, AI-077: a Ranged or casting Role keeps its distance); a
        // quickened cast keeps the routine going (PHB p.98).
        if (TryExecuteSpellcastAction(npc, closestPC))
        {
            yield return CombatPacing.Wait(0.8f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, closestPC, CombatStyle.Ranged));
                yield break;
            }
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
                    yield return CombatPacing.Wait(0.35f);
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
        // The keep-away distance (2 squares), widened by the traits' range adjust (batch 14 step 6, C3: Cowardly +2); the
        // Ranged-or-casting-only adjust applies only when the creature's Role is one (a melee Role reaches the kiter too).
        int keepAway = DND35.AI.Personality.AiBlend.KeepAwayDistance(npc, DND35.AI.Personality.AiBlend.LegacyKeepAwaySquares,
            DND35.AI.Personality.AiBlend.IsRangedOrCastingRole(profile));
        bool shouldRetreatForDistance = distToClosestPC <= keepAway && canMoveNow && !tookTacticalStep;
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
                    _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, retreatMove)));

                // ── Death/disable check after retreat movement ──
                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dropped during retreat movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!retreatMove.Refused) // a refused move spends nothing and the routine goes on (D3; the executor spent this one first)
                {
                    if (shouldRetreatForRisk)
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} repositions to avoid provoking attacks of opportunity."));
                    else
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} retreats to maintain distance!"));

                    yield return CombatPacing.Wait(0.5f);
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

        // After the move above, the move action is used only when it is still left (the routine's move was refused or
        // not wanted), and a quickened cast keeps the routine going (batch 14 step 11, AI-077).
        if (TryExecuteSpellcastAction(npc, rangedTarget))
        {
            yield return CombatPacing.Wait(0.8f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, rangedTarget, CombatStyle.Ranged));
                yield break;
            }
        }

        // The weapon attack goes only to a target the attack guard allows (batch 13 step 5, D1; AI-075): the reach or range
        // of the weapon in hand, its ammunition and load. A spell's range no longer widens the weapon's (the AI-075 cause:
        // GetBestAvailableSpellRangeInSquares let a staff "reach" the spell's range, and the attack then stopped with
        // "target is out of reach" while a foe stood next to the caster).
        CharacterController attackTarget = SelectGuardPassingTarget(npc, attackPool);
        if (attackTarget != null)
        {
            // The one maneuver try in place of the whole attack (batch 14 step 9: the scorer decides it against the attack).
            bool usedSpecial = TryExecuteChosenManeuver(npc, attackTarget, profile, wholeAttack: true);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, attackTarget));
            else
            {
                // A failed trip may be waiting on a controllable defender's counter-trip choice (CMB-079).
                while (_gameManager.IsAwaitingCounterTripChoice)
                    yield return null;
                yield return CombatPacing.Wait(0.8f);
            }
            yield break;
        }

        // No target in reach or range: the profile's alternatives (C1) through the catalog: another target, the preferred
        // range or an approach (then the attack with the standard action left, AI-045), or total defense.
        yield return _gameManager.StartCoroutine(RunOutOfReachAlternatives(npc, rangedTarget, CombatStyle.Ranged, allowThrow: true));
        yield return CombatPacing.Wait(0.3f);
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

        // The Role's multiplier, times 2 - rho for a creature with traits (batch 14 step 6, C7); the clamp stays. A creature
        // with neither a Role nor traits keeps the base tolerance.
        if (profile != null || npc.Personality != null)
        {
            float roleMultiplier = profile != null ? profile.GetRangedAoORiskToleranceMultiplier() : 1f;
            tolerancePercent *= Mathf.Clamp(DND35.AI.Personality.AiBlend.RiskToleranceFactor(npc, roleMultiplier), 0.25f, 2f);
        }

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

        AIProfile profile = GetProfile(npc);

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        // The withdraw it may take: a full-round withdraw, or a Restricted Withdraw (a standard action at speed) when it is
        // limited to a single action, never blinded or after a 5-foot step (PHB p.143-144; the shared legality, GRID-022).
        Legality withdrawLegality = hpPercent < 0.30f
            ? MovementService.CanStartMovement(npc, MovementService.WithdrawKindFor(npc))
            : Legality.Refused("not hurt enough");

        // A melee caster casts first, as in the aggressive melee turn (batch 13 step 8, AI-002; C4), but not when it is
        // badly hurt and may withdraw: the routine's withdraw (below 30% HP) comes first, and the cast is tried after a
        // withdraw that did not start.
        bool castFirstTried = false;
        if (!withdrawLegality.Ok)
        {
            castFirstTried = true;
            if (TryCastBeforeMelee(npc, target, profile))
            {
                yield return CombatPacing.Wait(0.8f);
                // The move action after the cast (batch 14 step 11, AI-077); a quickened cast keeps the routine going.
                if (!KeepsRoutineAfterAbility(npc))
                {
                    yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, target, CombatStyle.Melee));
                    yield break;
                }
            }
        }

        AIActionType action = SelectBestAction(npc, target, preferAggression: false);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target, ShouldNPCChargeBullRush(npc, target)));
            yield break;
        }

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
                    yield return CombatPacing.Wait(0.45f);
                    yield break;
                }
            }
        }

        // No withdraw was made: the cast-first step the withdraw test held back.
        if (!castFirstTried && TryCastBeforeMelee(npc, target, profile))
        {
            yield return CombatPacing.Wait(0.8f);
            if (!KeepsRoutineAfterAbility(npc))
            {
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, target, CombatStyle.Melee));
                yield break;
            }
        }

        // Moving needs the move action (AI-034).
        bool triedApproach = false;
        if (!npc.IsTargetInCurrentWeaponRange(target) && AiLegal.CanMoveNow(npc, _gameManager))
        {
            triedApproach = true;
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                var advanceMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, advanceMove)));

                // ── Death/disable check after movement ──
                if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
                if (!npc.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dropped during movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!advanceMove.Refused) // a refused move spends nothing and the routine goes on (D3; the executor spent this one first)
                {
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npc.Stats.CharacterName} advances methodically toward {target.Stats.CharacterName}."));
                    yield return CombatPacing.Wait(0.5f);
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
            // Aid another or total defense where it beats the attack (batch 14 step 13, AI-063, C8), as in the aggressive turn.
            if (ChooseMeleeTurnOptionForAI(npc, target, profile, out ActionContext alternative) != MeleeTurnOption.Attack)
            {
                var alternativeOutcome = new ActionOutcome();
                yield return _gameManager.StartCoroutine(RunMeleeTurnAlternative(npc, target, alternative, alternativeOutcome));
                if (alternativeOutcome.Performed || CombatEnded() || !npc.CanTakeTurnActions())
                    yield break;
            }

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
            yield return CombatPacing.Wait(0.3f);
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

        if (!swarm.IsTargetInCurrentWeaponRange(target) && AiLegal.CanMoveNow(swarm, _gameManager))
        {
            var swarmMove = new MoveAttempt();
            yield return _gameManager.StartCoroutine(
                _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(swarm, target.GridPosition, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, swarmMove)));

            // ── Death/disable check after swarm movement ──
            if (CombatEnded()) yield break; // victory or defeat already declared (CORE-011)
            if (!swarm.CanTakeTurnActions())
            {
                Debug.Log($"🔥 [AI] {swarm.Stats.CharacterName} dropped during swarm movement (HP={swarm.Stats.CurrentHP}) — turn ended");
                yield break;
            }

            if (!swarmMove.Refused) // a refused move spends nothing (D3; the executor spent this one first)
                yield return CombatPacing.Wait(0.35f);
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

        yield return CombatPacing.Wait(0.3f);

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

                yield return CombatPacing.Wait(0.25f);

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
                    yield return CombatPacing.Wait(0.2f);
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

    private static AIProfile GetProfile(CharacterController npc)
    {
        return npc != null ? npc.aiProfile : null;
    }

    /// <summary>
    /// The melee routines' cast-first step (batch 13 step 8, AI-002): when <paramref name="profile"/> (or, without one,
    /// <see cref="AIProfile.DefaultShouldTryCastBeforeMelee"/>) asks for it, the turn's spell through
    /// <see cref="TryExecuteSpellcastAction"/> (the catalog's cast entry), aimed by default at <paramref name="target"/>.
    /// The cast takes only the spells the gate counts (<see cref="AIProfile.IsCastFirstCandidate"/>: no Utility,
    /// Divination or out-of-combat spell, AI-069) and only one scoring above 0, so a melee caster never trades its attack
    /// for a spell that does nothing (an interim AI choice that batch 14's personalities and batch 22's evaluator
    /// replace). True when a spell was cast; false spends nothing.
    /// </summary>
    private bool TryCastBeforeMelee(CharacterController npc, CharacterController target, AIProfile profile)
    {
        if (npc == null || npc.Stats == null || npc.Actions == null || !npc.Actions.HasStandardAction)
            return false;
        bool wants = profile != null ? profile.ShouldTryCastBeforeMelee(npc) : AIProfile.DefaultShouldTryCastBeforeMelee(npc);
        if (!wants)
            return false;
        Debug.Log($"[AI][Spell] {npc.Stats.CharacterName} tries a spell before melee (AI-002)");
        return TryExecuteSpellcastAction(npc, target, AIProfile.IsCastFirstCandidate, minScore: 0f);
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
        // Decision time and trace (batch 14 step 1): timed and recorded only, the choice is unchanged.
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Target, npc);
        CharacterController chosen = SelectBestTargetBody(npc, allCombatants, filter);
        if (npc != null && AiDecisionTrace.Enabled)
        {
            string name = TraceName(chosen);
            AiDecisionTrace.Record(npc, "Target", name, name, 0f, filter != null ? "filtered" : null);
        }
        return chosen;
    }

    private CharacterController SelectBestTargetBody(CharacterController npc, List<CharacterController> allCombatants, Func<CharacterController, bool> filter)
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

            // The charm link (PHB p.209; batch 13 step 20, D14): the shared predicate the attack gate's Charm bit reads.
            if (CharmRules.IsCharmedBy(npc, candidate))
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
    /// only (AiAttackGuard.IntentFor), so a melee creature never picks a far foe to throw its weapon at. Null as well when
    /// the creature may declare no attack now (AiLegal.CanAttackNow: no attack action left, a nauseated or slowed turn;
    /// batch 14 step 10, AI-080), so the routine goes on to its out-of-reach alternatives instead of a refused attack.
    /// </summary>
    public CharacterController SelectGuardPassingTarget(CharacterController npc, List<CharacterController> allCombatants = null, bool allowThrow = true)
    {
        if (npc == null || _gameManager == null)
            return null;
        if (!AiLegal.CanAttackNow(npc, _gameManager))
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

        // The armor-priority switch comes before the profile (AI-053, batch 13 step 7): the only creatures that have it
        // (the armor-targeting test preset's skeleton archers) also have a profile, which made it unreachable.
        if (UsesArmorPriorityTargeting(npc))
        {
            CharacterController prioritized = SelectBestArmorPriorityTarget(npc, candidateTargets);
            if (prioritized != null)
                return prioritized;
        }

        AIProfile profile = GetProfile(npc);
        if (profile != null)
        {
            CharacterController profiled = SelectBestTargetFromProfile(npc, candidateTargets, profile);
            if (profiled != null)
                return profiled;
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

        // The Role's ignore-downed answer through the personality blend (batch 14 step 5): the traits may overrule it.
        bool ignoreUnconscious = hasConsciousEnemy
            && DND35.AI.Personality.AiBlend.IgnoresUnconscious(npc, profile.ShouldIgnoreUnconsciousTargets(npc));

        // With a personality, the trait term is added to the Role's score (C4) and the legacy choice is kept for the
        // trace's summary row; without one, the score is the Role's alone.
        bool tracePersonality = DND35.AI.Personality.AiBlend.TracesTargets(npc);
        CharacterController legacyBest = null;
        float legacyBestScore = float.NegativeInfinity;
        float bestTerm = 0f;
        for (int i = 0; i < enemyCandidates.Count; i++)
        {
            CharacterController candidate = enemyCandidates[i];
            if (ignoreUnconscious && candidate.IsUnconscious)
                continue;

            float roleScore = profile.ScoreTarget(candidate, npc);
            float perception = GetPerceptionTargetingAdjustment(npc, candidate, profile);
            float score = DND35.AI.Personality.AiBlend.ScoreTarget(npc, candidate, roleScore) + perception;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                bestTerm = score - (roleScore + perception);
            }
            if (tracePersonality && roleScore + perception > legacyBestScore)
            {
                legacyBestScore = roleScore + perception;
                legacyBest = candidate;
            }
        }
        if (tracePersonality)
            DND35.AI.Personality.AiBlend.TraceTarget(npc, "Target", TraceName(best), TraceName(legacyBest), bestTerm);

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
        bool tracePersonality = DND35.AI.Personality.AiBlend.TracesTargets(npc);
        CharacterController legacyBest = null;
        float legacyBestScore = float.NegativeInfinity;
        float bestTerm = 0f;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(npc, candidate))
                continue;

            float rolePriority = GetTargetPriority(npc, candidate);
            float perception = GetPerceptionTargetingAdjustment(npc, candidate);
            // The trait term on the no-profile path too (batch 14 step 5, C4); the priority alone without a personality.
            float score = DND35.AI.Personality.AiBlend.ScoreTarget(npc, candidate, rolePriority) + perception;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                bestTerm = score - (rolePriority + perception);
            }
            if (tracePersonality && rolePriority + perception > legacyBestScore)
            {
                legacyBestScore = rolePriority + perception;
                legacyBest = candidate;
            }
        }
        if (tracePersonality)
            DND35.AI.Personality.AiBlend.TraceTarget(npc, "Target", TraceName(best), TraceName(legacyBest), bestTerm);

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

            // In reach or range: the attack guard (D1; reach, footprint, range, ammunition), not an anchor-to-anchor distance
            // against 1 square for every melee weapon, which dropped a Large target's adjacent footprint and a reach
            // weapon's 10-ft targets. The distance bonus measures footprint to footprint.
            if (!AiAttackGuard.Passes(npc, candidate))
                continue;
            int distance = npc.GetMinimumDistanceToTarget(candidate, chebyshev: false);

            float armorScore = GetArmorPriorityScore(npc, candidate);
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

    /// <summary>The "Uses Armor-Based Targeting" switch, translated from the control tag at spawn (CharacterController.AIControl; batch 13 step 7).</summary>
    private static bool UsesArmorPriorityTargeting(CharacterController npc)
        => npc != null && (npc.AIControl & DND35.AI.Knowledge.AIControlFlags.ArmorPriorityTargeting) != 0;

    /// <summary>
    /// The armor-priority score of <paramref name="target"/> as <paramref name="npc"/> knows it (the ArmorCategory fact
    /// through the knowledge provider; batch 13 step 7): unarmored 100, light 75, medium 50, heavy 25, anything else 10.
    /// </summary>
    private static float GetArmorPriorityScore(CharacterController npc, CharacterController target)
    {
        if (target == null)
            return 0f;

        DND35.AI.Knowledge.FactValue armor = DND35.AI.Knowledge.KnowledgeService.ViewOf(npc, target).Get(DND35.AI.Knowledge.FactKey.ArmorCategory);
        if (!armor.IsKnown)
            return 10f;
        switch ((DND35.AI.Knowledge.WornArmor)armor.Number)
        {
            case DND35.AI.Knowledge.WornArmor.Unarmored: return 100f;
            case DND35.AI.Knowledge.WornArmor.Light: return 75f;
            case DND35.AI.Knowledge.WornArmor.Medium: return 50f;
            case DND35.AI.Knowledge.WornArmor.Heavy: return 25f;
            default: return 10f;
        }
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
        // The one position store (AI-021): a target seen now is recorded; one seen or pinpointed before is tracked.
        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        if (tracker != null)
            tracker.RecordSighting(target, incomingIsRanged);

        int missChance = target.GetMissChance(npc, incomingIsRanged);
        bool canSeeTarget = npc.CanSee(target, incomingIsRanged);
        bool hasLastKnownPosition = tracker != null && tracker.HasLastKnownPosition(target);

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

        // Flanking (PHB p.153): the creature and an ally threaten the target from opposite sides, where it stands now
        // (CombatUtils.IsAttackerFlanking, the attack's own test). Until batch 13 step 7 this term only tested threat (AI-053).
        bool flanking = CombatUtils.IsAttackerFlanking(npc, target, _gameManager != null ? _gameManager.GetAllCharactersForAI() : null, out _);
        float flankingBonus = flanking ? 2.5f : 0f;

        // The CreatureType fact through the knowledge provider (batch 13 step 7).
        DND35.AI.Knowledge.ITargetView view = DND35.AI.Knowledge.KnowledgeService.ViewOf(npc, target);
        bool vulnerableType = view.Has(DND35.AI.Knowledge.FactKey.CreatureType, "Undead")
            || view.Has(DND35.AI.Knowledge.FactKey.CreatureType, "Outsider");
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
    /// <paramref name="rejectCell"/>, when given, rules a destination out (the move after a standard action refuses a
    /// square that would end a turning, batch 14 step 11). <paramref name="rejectProvokingPaths"/> rules out a destination
    /// whose path provokes (the healer's partial approach when its price forbids provoking, batch 14 step 12, Q8), tested
    /// on the path the search computes anyway.
    /// </summary>
    public SquareCell EvaluateMovementOptions(CharacterController mover, Vector2Int targetPos, bool retreat, CharacterController targetCharacter = null, AIProfile profile = null, int? preferredRangeOverride = null, Func<Vector2Int, bool> rejectCell = null, bool rejectProvokingPaths = false)
    {
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Movement, mover);
        if (mover == null || mover.Stats == null || _gameManager.Grid == null)
            return null;

        if (profile == null)
            profile = GetProfile(mover);

        int moverMoveRange = _gameManager.GetCurrentMoveRangeSquares(mover);
        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(mover.GridPosition, moverMoveRange);
        List<CharacterController> allCombatants = targetCharacter != null ? _gameManager.GetAllCharactersForAI() : null;

        SquareCell bestCell = null;
        float bestScore = float.NegativeInfinity;

        // The Role's preferred range, adjusted by the traits (batch 14 step 6, C3: Cowardly +2 on a Ranged, Mixed or casting
        // Role); an override (the approach to the weapon's reach or range) is the caller's and stays as given.
        int preferredRange = preferredRangeOverride.HasValue
            ? Mathf.Max(0, preferredRangeOverride.Value)
            : profile != null && profile.Movement != null
                ? Mathf.Max(0, DND35.AI.Personality.AiBlend.PreferredRange(mover, profile.Movement.PreferredRangeSquares,
                    DND35.AI.Personality.AiBlend.IsRangedOrCastingRole(profile)))
                : 1;

        bool avoidAoOs = profile != null
            && profile.Movement != null
            && profile.Movement.AvoidAoOs
            && !profile.ShouldIgnoreAoO(mover);
        // The price per provoker of a path that provokes (C6): the Role's (1000, a flat cost, when it avoids AoOs; else 2),
        // priced by the traits' ProvokeAoO vote (batch 14 step 6); AiBlend.AoOPenalty turns it into the path's penalty.
        float aooPrice = DND35.AI.Personality.AiBlend.AoOCost(mover,
            avoidAoOs ? DND35.AI.Personality.AiBlend.AoOForbidPrice : DND35.AI.Personality.AiBlend.AoOForCeiling);
        bool seekFlanking = profile == null || profile.Movement == null || profile.Movement.SeekFlanking;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, mover.GetVisualSquaresOccupied(), mover))
                continue;
            if (rejectCell != null && rejectCell(cell.Coords))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(mover, cell.Coords, avoidThreats: false, maxRangeOverride: moverMoveRange);
            if (pathResult == null || pathResult.Path == null)
                continue;
            if (rejectProvokingPaths && pathResult.ProvokesAoOs)
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
                score -= DND35.AI.Personality.AiBlend.AoOPenalty(aooPrice, pathResult.ProvokedAoOs.Count);

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

    /// <summary>
    /// Whether the AI would replace <paramref name="npc"/>'s next attack step on <paramref name="target"/> with a maneuver
    /// or a coup de grace: DND35.AI.AiManeuverChooser.Decide (batch 14 step 9: the Role proposes, the scorer decides against
    /// the attack). A thin wrapper for the static suites; it executes nothing.
    /// </summary>
    public bool ShouldUseManeuver(CharacterController npc, CharacterController target)
        => DND35.AI.AiManeuverChooser.Decide(npc, target, GetProfile(npc)).UsesManeuver;

    /// <summary>
    /// Attack action whose melee steps the AI may each replace with a maneuver (CMB-102): before every
    /// step the maneuver choice runs (<see cref="CreateMeleeStepManeuverEvaluator"/>: a coup de grace,
    /// else the Role's proposal when its odds and value beat the attack it replaces, batch 14 step 9;
    /// AI-035, AI-060). Trip, disarm, sunder and grapple use that step at its BAB
    /// (ManeuverActionCost.ReplacesMeleeAttack); other maneuvers spend their own action and end it.
    /// </summary>
    private IEnumerator PerformMeleeAttackActionWithManeuvers(CharacterController npc, CharacterController target, AIProfile profile)
    {
        // A cleric's domain powers before the attack (batch 14 step 14, D7, C14; AIProfile.ChooseDomainPower): the feat of
        // strength (free) when not yet used, and the Death domain's touch, which takes the standard action in place of the
        // attack (a supernatural ability, PHB p.142, p.186) when its value beats the attack's. After the touch the move left
        // follows (D1), as after aid another.
        DomainPowerChoice domainPower = _gameManager.TryNPCDomainPowerForAI(npc, DomainPowerSite.MeleeAttack, target);
        if (domainPower == DomainPowerChoice.Death)
        {
            if (CombatEnded() || npc.Stats == null || !npc.CanTakeTurnActions())
                yield break;
            yield return CombatPacing.Wait(0.5f);
            if (!KeepsRoutineAfterAbility(npc))
                yield return _gameManager.StartCoroutine(UseMoveAfterStandardAction(npc, target, CombatStyle.Melee));
            yield break;
        }
        if (domainPower != DomainPowerChoice.None && (CombatEnded() || npc.Stats == null || !npc.CanTakeTurnActions()))
            yield break;

        // A feint before the attack (PHB p.155; a move action with Improved Feint, PHB p.95; CMB-102): an AI stopgap, by the
        // profile (AIProfile.ShouldFeint; by default a rogue with Improved Feint that makes one attack anyway). The feint is
        // the NPC row of the maneuver core (TryNPCSpecialAttackByTypeForAI, the PC's cost table).
        bool feint = profile != null ? profile.ShouldFeint(npc, target) : AIProfile.DefaultShouldFeint(npc, target);
        if (feint && _gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.Feint))
        {
            if (CombatEnded() || npc.IsDead || !npc.CanTakeTurnActions())
                yield break;
            yield return CombatPacing.Wait(0.3f);
        }

        yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(
            npc,
            target,
            CreateMeleeStepManeuverEvaluator(profile)));
    }

    /// <summary>
    /// The per-step maneuver evaluation PerformMeleeAttackActionWithManeuvers hands to the NPC melee sequence
    /// (GameManager.RunAttackSequence through NPCPerformAttack): DND35.AI.AiManeuverChooser.Decide, then the chosen maneuver
    /// through the NPC executor. Inside the sequence's step hook (GameManager.ManeuverStepIntentForAI set) a maneuver
    /// replaces one step; called outside it (NPCPerformAttack's one try before a ranged or thrown sequence) it replaces the
    /// whole attack. Internal so the static suites can run the real evaluation through the loop.
    /// </summary>
    internal System.Func<CharacterController, CharacterController, bool> CreateMeleeStepManeuverEvaluator(AIProfile profile)
        => (actor, stepTarget) => TryExecuteChosenManeuver(actor, stepTarget, profile,
            wholeAttack: _gameManager == null || _gameManager.ManeuverStepIntentForAI == null);

    /// <summary>
    /// Batch 14 step 9 (AI-035, AI-060, AI-058, AI-025): the step's maneuver choice (DND35.AI.AiManeuverChooser.Decide: a
    /// coup de grace, else the Role's proposal when it beats the attack it replaces by its odds and value) and its execution
    /// (GameManager.TryNPCSpecialAttackByTypeForAI). True when a maneuver was made (its cost spent); false leaves the step an
    /// attack. No per-turn memory: the AI-060 stopgap (AIManeuverTurnMemory) is gone, so each step is decided again.
    /// </summary>
    private bool TryExecuteChosenManeuver(CharacterController npc, CharacterController target, AIProfile profile, bool wholeAttack)
    {
        if (npc == null || target == null || _gameManager == null)
            return false;

        // Only the choice is timed (inside Decide); the maneuver's execution stays outside every timing scope.
        DND35.AI.ManeuverDecision decision = DND35.AI.AiManeuverChooser.Decide(npc, target, profile, wholeAttack);
        SpecialAttackType? chosen = decision.Chosen;
        if (!chosen.HasValue)
            return false;
        if (_gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, chosen.Value))
            return true;
        // The executor refused the decided maneuver (its cost, reach or own gate): the step stays an attack, and the decision
        // trace says so after the Decide row.
        DND35.AI.AiManeuverChooser.TraceRefused(npc, chosen.Value);
        return false;
    }

    /// <summary>
    /// Whether the AI-run <paramref name="npc"/> takes a coup de grace (PHB p.153) when the rules allow one: the one copy
    /// of the choice (batch 14 step 5 merged AIService.ShouldNPCUseCoupDeGrace and the test in
    /// GameManager.HasNPCCoupDeGraceOption). The definition's data override (CharacterController.EnemyUseCoupDeGraceOverride)
    /// wins, then the traits' vote (DND35.AI.Personality.AiBlend.UsesCoupDeGrace: a sadist insists at Normal difficulty
    /// and above, never on Easy, owner 2026-10-30), then the Role (AIProfile.ShouldUseCoupDeGrace; no profile: no). A
    /// choice only: the legality is the caller's (GameManager.CanUseCoupDeGraceAttackOption, or the adjacent helpless foes
    /// and the full-round action).
    /// </summary>
    internal static bool UsesCoupDeGrace(CharacterController npc)
    {
        if (npc == null)
            return false;

        bool? dataOverride = npc.EnemyUseCoupDeGraceOverride;
        AIProfile profile = GetProfile(npc);
        bool roleLegacy = !dataOverride.HasValue && profile != null && profile.ShouldUseCoupDeGrace(npc);
        return DND35.AI.Personality.AiBlend.UsesCoupDeGrace(npc, roleLegacy, dataOverride);
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
        // Each step of the walk is timed (batch 14 step 1); the consumer's cast between two candidates is not.
        => AiDecisionTimer.Timed(RankedSpellCandidatesBody(caster, fallbackTarget, scored), AiTimeBucket.Spell, caster);

    private IEnumerable<AiSpellCandidate> RankedSpellCandidatesBody(CharacterController caster, CharacterController fallbackTarget, List<AiScoredSpell> scored)
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
        if (!AiLegal.CanMoveNow(caster, _gameManager))
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

        if (target == null && AiLegal.CanMoveNow(npc, _gameManager) && reachable.Count > 0)
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
                CharacterController candidate = reachable[i];
                // The executor spends the move before the first step (D3, batch 13 step 10) and shows the line only then.
                var touchMove = new MoveAttempt();
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(MoveRequest.ForAI(npc, approach.Value, _gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, touchMove)
                        .LoggingOnStart(CombatLogHelper.Info("✋", $"{npc.Stats.CharacterName} moves to touch {candidate.Stats.CharacterName} with the held {held.Name}."))));
                if (CombatEnded() || ThreatSystem.IsMoverIncapacitated(npc))
                    yield break; // dropped by an attack of opportunity on the way (CMB-073)
                if (touchMove.Refused)
                {
                    // Refused before the first step, nothing spent (D3): with no path the next creature in reach is tried;
                    // a legality refusal (logged) refuses every move this turn.
                    if (touchMove.NoPath)
                        continue;
                    yield break;
                }
                target = candidate;
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
        // Through the catalog's deliver-held-charge entry (batch 13 step 6), whose executor runs TryNPCDeliverHeldChargeForAI.
        _gameManager.RunAIActionNow(ActionContext.For(ActionIds.DeliverHeldCharge, npc, ActorSource.AI, target));
        yield return CombatPacing.Wait(0.5f);
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

    /// <summary>The "AI:MagicMissileOnly" switch, translated from the creature's control tag at spawn (CharacterController.AIControl; batch 13 step 7).</summary>
    private static bool IsMagicMissileOnlyCaster(CharacterController caster)
        => caster != null && (caster.AIControl & DND35.AI.Knowledge.AIControlFlags.MagicMissileOnly) != 0;

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
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Spell, caster);
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
        // Decision time and trace (batch 14 step 1): timed and recorded only, the choice is unchanged.
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Action, npc);
        AIActionType chosen = SelectBestActionBody(npc, target, preferAggression);
        if (npc != null && AiDecisionTrace.Enabled)
        {
            string name = chosen.ToString();
            AiDecisionTrace.Record(npc, "Action", name, name, 0f, preferAggression ? "aggressive" : null);
        }
        return chosen;
    }

    private AIActionType SelectBestActionBody(CharacterController npc, CharacterController target, bool preferAggression)
    {
        if (npc == null || target == null)
            return AIActionType.Wait;

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        bool canCharge = _gameManager.ShouldNPCUseChargeForAI(npc, target);

        AIProfile profile = GetProfile(npc);
        bool profilePrefersCharge = profile == null || profile.ShouldPreferCharge(npc, target, distance, preferAggression);
        // The traits vote on a legal charge only (batch 14 step 6, C5: the rules gate first): Cowardly and Caster forbid
        // it, a Healer is against it, a Berserker insists; with no traits the Role's answer stands.
        bool prefersCharge = canCharge ? DND35.AI.Personality.AiBlend.PrefersCharge(npc, profilePrefersCharge) : profilePrefersCharge;

        Debug.Log($"[AI SelectBestAction] {npc.Stats.CharacterName} → {target.Stats.CharacterName}: dist={distance}, canCharge={canCharge}, profilePrefersCharge={profilePrefersCharge}, prefersCharge={prefersCharge}, hpPct={hpPercent:F2}");

        if (!preferAggression && hpPercent < 0.30f && npc.Actions.HasMoveAction)
            return AIActionType.Retreat;

        if (canCharge && prefersCharge)
            return AIActionType.Charge;

        // In reach: an attack. Whether a step becomes a maneuver is decided per step by the attack sequence's evaluator
        // (AiManeuverChooser, batch 14 step 9); no caller reads SpecialManeuver, so the choice is not made twice here.
        if (npc.IsTargetInCurrentWeaponRange(target))
            return AIActionType.Attack;

        if (distance > 1 && npc.Actions.HasMoveAction)
            return AIActionType.Move;

        return AIActionType.Wait;
    }

    private CharacterController SelectLowestHPEnemy(CharacterController npc)
    {
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Target, npc);
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
            yield return CombatPacing.Wait(0.3f);
            yield break;
        }

        Vector2Int targetPos = target != null ? target.GridPosition : npc.GridPosition;
        Vector2Int? bestWallCell = _gameManager.FindBestAdjacentWallCellForAI(npc, targetPos);

        if (!bestWallCell.HasValue)
        {
            yield return CombatPacing.Wait(0.3f);
            yield break;
        }

        WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(bestWallCell.Value);
        if (wall == null || wall.IsBreached(bestWallCell.Value))
        {
            yield return CombatPacing.Wait(0.3f);
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
            yield return CombatPacing.Wait(0.3f);
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
            if (!AttackLegality.Check(source, candidate, DirectAttackKind.Touch, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward | AttackLegalityChecks.Charm).Ok)
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
            if (!AttackLegality.Check(summon, candidate, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward | AttackLegalityChecks.Charm).Ok)
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
        using AiDecisionTimer.Scope timing = AiDecisionTimer.Open(AiTimeBucket.Target, attacker);
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
                    AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward | AttackLegalityChecks.Charm).Ok)
                continue;

            enemies.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = hasConsciousEnemy
            && DND35.AI.Personality.AiBlend.IgnoresUnconscious(attacker, profile != null && profile.ShouldIgnoreUnconsciousTargets(attacker));

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
        // The trait term (batch 14 step 5, C4) and, with a personality, one Personality.AdaptiveTarget summary row.
        bool tracePersonality = DND35.AI.Personality.AiBlend.TracesTargets(attacker);
        CharacterController legacyBest = null;
        float legacyBestScore = float.NegativeInfinity;
        float bestTerm = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController candidate = candidates[i];
            float roleScore = profile != null ? profile.ScoreTarget(candidate, attacker) : 0f;
            float score = DND35.AI.Personality.AiBlend.ScoreTarget(attacker, candidate, roleScore);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                bestTerm = score - roleScore;
            }
            if (tracePersonality && roleScore > legacyBestScore)
            {
                legacyBestScore = roleScore;
                legacyBest = candidate;
            }
        }
        if (tracePersonality)
            DND35.AI.Personality.AiBlend.TraceTarget(attacker, "AdaptiveTarget", best, legacyBest, bestTerm);

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
        // Through the catalog's ranged special or spittle entry (batch 13 step 6), whose executor runs the core below.
        RangedSpecialAttackDefinition definition = npc.GetRangedSpecialAttackDefinition();
        string id = definition != null && definition.IsFreeAction ? ActionIds.Spittle : ActionIds.RangedSpecial;
        // The entry's legality first (batch 14 step 10, AI-080), for every caller (the turn-start spittle and the ranged
        // special before or after the move): a pick its core would refuse is no pick.
        if (!AiLegal.CanUse(npc, id, _gameManager))
            return false;
        return _gameManager.RunAIActionNow(ActionContext.For(id, npc, ActorSource.AI, target)).Performed;
    }

    /// <summary>The ranged special attack core (the catalog's executor, GameManager.ActionCore_RangedSpecial).</summary>
    internal bool ExecuteRangedSpecialAttackCore(CharacterController npc, CharacterController target)
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

        // The spittle is a free action every round (MM p.126): once per round (batch 13 step 18, D11).
        if (attack.IsFreeAction && npc.FreeRangedSpecialUsedThisRound)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName}'s {attack.Name} was already used this round.");
            return false;
        }

        // A creature that cannot attack (nauseated, PHB p.311), has no further action, or is grappling makes no ranged
        // touch attack, free or not: the executors' shared bar, repeated so the core cannot be reached around them.
        Legality? actorBar = MonsterAbilityRules.ActorRefusal(npc, attack.Name);
        if (actorBar.HasValue)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} cannot use {attack.Name}: {actorBar.Value.Reason}");
            return false;
        }

        // A ranged touch attack is an attack, so a holder may not make it against the creature it holds with one body
        // part (MM p.310; delegated RAW reading 2026-10-14, CMB-163).
        if (npc.IsBarredFromOrdinaryAttackOn(target, out string holdReason))
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} cannot use {attack.Name} on {target.Stats.CharacterName}: {holdReason}");
            return false;
        }

        // Space to space, the PHB p.147 count (a Large creature measures from its nearest square; batch 13 step 18 review).
        if (!RangedSpecialActionExecutor.IsInRange(npc, target, attack))
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName}'s target is too far ({npc.GetMinimumDistanceToTarget(target, chebyshev: false)} squares) for {attack.Name} (range {attack.RangeFeet} ft).");
            return false;
        }

        // Nothing passes a Resilient Sphere (CMB-092), and a known failed ward save bars the target (D6); read only.
        Legality specialLegality = AttackLegality.Check(npc, target, DirectAttackKind.Touch, null,
            AttackLegalityChecks.Sphere | AttackLegalityChecks.Ward | AttackLegalityChecks.Charm);
        if (!specialLegality.Ok)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} cannot use {attack.Name} on {target.Stats.CharacterName}: {specialLegality.Reason}");
            return false;
        }

        // The spittle's round is spent once it is fired, a ward's failure included (it loses the attack, as below).
        if (attack.IsFreeAction)
            npc.SetFreeRangedSpecialUsedThisRound(true);

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

        // An attack on a creature charmed by the attacker or its apparent allies ends the charm (PHB p.209; batch 12 D15),
        // as the weapon attacks do; then the one combat-end check for both sides, so a spittle that drops the last foe
        // ends the fight on the PC's turn as on the AI's (batch 13 step 18 review).
        _gameManager.BreakCharmOnHostileAction(npc, target);
        _gameManager.EvaluateCombatEnd("RangedSpecial");

        return true;
    }

    /// <summary>
    /// Attempt to execute an Engulf attack. Returns true if engulf was attempted.
    /// </summary>
    public bool TryExecuteEngulf(CharacterController npc, CharacterController target)
    {
        if (_gameManager == null || npc == null || target == null || npc.Stats == null)
            return false;
        // The engulf entry's legality first (batch 14 step 10, AI-080): no standard action (a nauseated engulfer has a
        // single move action only, PHB p.310), no pick.
        if (!AiLegal.CanUse(npc, ActionIds.Engulf, _gameManager))
            return false;
        // Through the catalog's engulf entry (batch 13 step 6), whose executor runs the core below.
        return _gameManager.RunAIActionNow(ActionContext.For(ActionIds.Engulf, npc, ActorSource.AI, target)).Performed;
    }

    /// <summary>The engulf core (the catalog's executor, GameManager.ActionCore_Engulf).</summary>
    internal bool ExecuteEngulfCore(CharacterController npc, CharacterController target)
    {
        if (_gameManager == null || npc == null || target == null || npc.Stats == null)
            return false;

        EngulfDefinition engulf = npc.GetEngulfDefinition();
        if (engulf == null)
            return false;

        // A Large or smaller creature adjacent to the engulfer's space (MM p.202; EngulfActionExecutor.TargetRefusal), and an
        // engulfer free to act (the executors' shared bar).
        Legality? engulfBar = MonsterAbilityRules.ActorRefusal(npc, "Engulf");
        Legality engulfTarget = EngulfActionExecutor.TargetRefusal(npc, target);
        if (engulfBar.HasValue || !engulfTarget.Ok)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} cannot engulf {target.Stats.CharacterName}: {(engulfBar.HasValue ? engulfBar.Value.Reason : engulfTarget.Reason)}");
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
        
        // A threatening act: it ends a charm the engulfer or its apparent allies hold on the target (PHB p.209; D15).
        _gameManager.BreakCharmOnHostileAction(npc, target);

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

    // ── Aura Save Immunity Tracking ──
    // Per MM rules (e.g., Gibbering, MM p.126): successful save = 24-hour immunity (GameManager.ProcessTurnStartAura).
    // Frightful presence keeps its own ledger (FrightfulPresenceLedger, batch 13 step 19).
    // Key format: "AuraImmunity_{targetInstanceId}_{sourceInstanceId}_{auraName}"
    // We track this as a simple HashSet since combat encounters don't last 24 hours;
    // immunity persists for the remainder of the encounter.

    private static readonly HashSet<string> _auraSaveImmunities = new HashSet<string>();

    private static string GetAuraImmunityKey(CharacterController target, CharacterController source, string auraName)
    {
        return $"AuraImmunity_{target.GetInstanceID()}_{source.GetInstanceID()}_{auraName}";
    }

    internal static bool HasAuraSaveImmunity(CharacterController target, CharacterController source, string auraName)
    {
        return _auraSaveImmunities.Contains(GetAuraImmunityKey(target, source, auraName));
    }

    internal static void GrantAuraSaveImmunity(CharacterController target, CharacterController source, string auraName)
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
    internal static CombatConditionType AuraEffectToCondition(AuraEffectType effect)
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
