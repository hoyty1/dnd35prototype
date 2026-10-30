using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class TurnUndeadSystem : BaseCombatManeuver
{
}

public partial class GameManager
{
    private const int TurnUndeadFearBreakDistanceSquares = 2; // 10 ft in D&D 3.5e square grid.

    private sealed class TurnUndeadTracker
    {
        public CharacterController Turner;
        public float AppliedTime;
    }

    // Tracks which cleric is the source of an active Turned condition for each undead target.
    private readonly Dictionary<CharacterController, TurnUndeadTracker> _activeTurnUndeadTrackers = new Dictionary<CharacterController, TurnUndeadTracker>();

    // The PC's turning preview (the Turn Undead button, or the Sun domain's greater turning): who turns, and whether the
    // click runs a greater turning. The turning itself is the catalog's executor (batch 13 step 16, D9).
    private bool _isSelectingTurnUndead;
    private CharacterController _turnUndeadPendingInvoker;
    private bool _turnUndeadPendingGreater;

    public int GetRemainingTurnUndeadAttempts(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return 0;

        return Mathf.Max(0, actor.Stats.MaxTurnUndeadAttemptsPerDay - actor.Stats.TurnUndeadAttemptsUsedToday);
    }

    public bool IsTurnedUndead(CharacterController undead)
    {
        if (undead == null || undead.Stats == null || undead.Stats.IsDead)
            return false;

        int rounds = _conditionService != null
            ? _conditionService.GetConditionDuration(undead, CombatConditionType.Turned)
            : 0;

        if (rounds == 0)
            return undead.HasCondition(CombatConditionType.Turned);

        return rounds > 0 || rounds < 0;
    }

    public int GetTurnedRoundsRemaining(CharacterController undead)
    {
        if (!IsTurnedUndead(undead))
            return 0;

        if (_conditionService != null)
            return Mathf.Max(0, _conditionService.GetConditionDuration(undead, CombatConditionType.Turned));

        StatusEffect turnedCondition = GetActiveTurnedCondition(undead);
        return turnedCondition != null ? Mathf.Max(0, turnedCondition.RemainingRounds) : 0;
    }

    public CharacterController GetTurningCleric(CharacterController undead)
    {
        if (!IsTurnedUndead(undead))
            return null;

        CharacterController trackedSource = _conditionService != null
            ? _conditionService.GetConditionSource(undead, CombatConditionType.Turned)
            : null;
        if (trackedSource != null)
            return trackedSource;

        CharacterController recorded = GetTurnUndeadTurner(undead);
        if (recorded != null)
            return recorded;

        StatusEffect turnedCondition = GetActiveTurnedCondition(undead);
        if (turnedCondition == null || string.IsNullOrWhiteSpace(turnedCondition.SourceName))
            return null;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (string.Equals(candidate.Stats.CharacterName, turnedCondition.SourceName, StringComparison.Ordinal))
            {
                RegisterTurnUndeadTracker(undead, candidate);
                return candidate;
            }
        }

        return null;
    }

    private void RegisterTurnUndeadTracker(CharacterController undead, CharacterController turner)
    {
        if (undead == null)
            return;

        if (turner == null)
        {
            _activeTurnUndeadTrackers.Remove(undead);
            return;
        }

        _activeTurnUndeadTrackers[undead] = new TurnUndeadTracker
        {
            Turner = turner,
            AppliedTime = Time.time
        };
    }

    private CharacterController GetTurnUndeadTurner(CharacterController undead)
    {
        if (undead == null)
            return null;

        if (_activeTurnUndeadTrackers.TryGetValue(undead, out TurnUndeadTracker tracker))
            return tracker != null ? tracker.Turner : null;

        return null;
    }

    private bool IsTurnedBy(CharacterController undead, CharacterController candidateTurner)
    {
        if (undead == null || candidateTurner == null)
            return false;

        CharacterController recordedTurner = GetTurnUndeadTurner(undead);
        return recordedTurner != null && recordedTurner == candidateTurner;
    }

    private StatusEffect GetActiveTurnedCondition(CharacterController undead)
    {
        if (undead == null)
            return null;

        List<StatusEffect> conditions = undead.GetActiveConditions();
        for (int i = 0; i < conditions.Count; i++)
        {
            StatusEffect condition = conditions[i];
            if (condition != null && ConditionRules.Normalize(condition.Type) == CombatConditionType.Turned)
                return condition;
        }

        return null;
    }

    private void ClearTurnUndeadEffect(CharacterController undead, string logMessage = null)
    {
        if (undead == null)
            return;

        _activeTurnUndeadTrackers.Remove(undead);

        bool removed = _conditionService != null
            ? _conditionService.RemoveCondition(undead, CombatConditionType.Turned)
            : undead.RemoveCondition(CombatConditionType.Turned);
        if (removed)
        {
            Debug.Log($"[Turn Undead] Cleared turned condition on {undead.Stats?.CharacterName ?? "Unknown"}");
        }

        if (!string.IsNullOrWhiteSpace(logMessage))
            CombatUI?.ShowCombatLog(logMessage);
    }

    private void BreakTurnUndeadFear(CharacterController undead, CharacterController turner, string reason)
    {
        if (undead == null || turner == null || !undead.HasCondition(CombatConditionType.Turned) || !IsTurnedBy(undead, turner))
            return;

        string reasonText = reason == "melee"
            ? $"{turner.Stats.CharacterName} made a melee attack"
            : $"{turner.Stats.CharacterName} approached within 10 feet";

        ClearTurnUndeadEffect(undead, $"[Turn Undead] {undead.Stats.CharacterName}'s fear breaks! ({reasonText})");
        UpdateAllStatsUI();
        Debug.Log($"[Turn Undead] Fear broken for {undead.Stats?.CharacterName ?? "Unknown"} ({reasonText})");
    }

    private void CheckTurnUndeadProximityBreakingForCleric(CharacterController cleric)
    {
        if (cleric == null || cleric.Stats == null || cleric.Stats.IsDead)
            return;

        var toBreak = new List<CharacterController>();

        foreach (var kvp in _activeTurnUndeadTrackers)
        {
            CharacterController undead = kvp.Key;
            TurnUndeadTracker tracker = kvp.Value;

            if (undead == null || tracker == null)
                continue;
            if (!undead.HasCondition(CombatConditionType.Turned))
                continue;
            if (tracker.Turner != cleric)
                continue;

            int distanceSquares = cleric.GetMinimumDistanceToTarget(undead, chebyshev: true);
            if (distanceSquares <= TurnUndeadFearBreakDistanceSquares)
                toBreak.Add(undead);
        }

        for (int i = 0; i < toBreak.Count; i++)
            BreakTurnUndeadFear(toBreak[i], cleric, "proximity");
    }

    private void CheckTurnUndeadProximityBreakingForMover(CharacterController mover)
    {
        if (mover == null || mover.Stats == null || mover.Stats.IsDead)
            return;

        // Rule: only the specific turning cleric can break their own turning effect by proximity.
        CheckTurnUndeadProximityBreakingForCleric(mover);
    }

    private void ProcessTurnUndeadMeleeFearBreak(CharacterController attacker, CharacterController target, bool isMeleeAttack)
    {
        if (!isMeleeAttack || attacker == null || target == null)
            return;

        if (!target.HasCondition(CombatConditionType.Turned))
            return;

        if (IsTurnedBy(target, attacker))
            BreakTurnUndeadFear(target, attacker, "melee");
    }

    private void PruneTurnUndeadTrackers()
    {
        if (_activeTurnUndeadTrackers.Count == 0)
            return;

        var keysToRemove = new List<CharacterController>();
        foreach (var kvp in _activeTurnUndeadTrackers)
        {
            CharacterController undead = kvp.Key;
            TurnUndeadTracker tracker = kvp.Value;

            bool invalid = undead == null
                || undead.Stats == null
                || undead.Stats.IsDead
                || !undead.HasCondition(CombatConditionType.Turned)
                || tracker == null
                || tracker.Turner == null;

            if (invalid)
                keysToRemove.Add(undead);
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            _activeTurnUndeadTrackers.Remove(keysToRemove[i]);
    }

    private void LogOngoingTurnUndeadStatusAtRoundStart()
    {
        if (_activeTurnUndeadTrackers.Count == 0)
            return;

        foreach (var kvp in _activeTurnUndeadTrackers)
        {
            CharacterController undead = kvp.Key;
            TurnUndeadTracker tracker = kvp.Value;
            if (undead == null || tracker == null || tracker.Turner == null)
                continue;

            StatusEffect turnedCondition = GetActiveTurnedCondition(undead);
            if (turnedCondition == null)
                continue;

            CombatUI?.ShowCombatLog(CombatLogHelper.Death("", $"[Turn Undead] {undead.Stats.CharacterName} continues fleeing ({Mathf.Max(0, turnedCondition.RemainingRounds)} rounds remaining)"));
        }
    }

    public bool CanUseTurnUndead(CharacterController actor, out string reason)
    {
        reason = "Unavailable";
        if (actor == null || actor.Stats == null)
        {
            reason = "No active character";
            return false;
        }

        // A cleric, or a paladin from 4th paladin level (PHB p.44), by class level, not total level (CHR-020).
        if (!actor.Stats.CanEverTurnUndead)
        {
            reason = actor.Stats.IsPaladin ? "Paladin level 4 required" : "Cleric or Paladin (Lv4+) only";
            return false;
        }

        if (actor.Actions == null || !actor.Actions.HasStandardAction)
        {
            reason = "Standard action used";
            return false;
        }

        // Turning is a supernatural standard action, which is not among the actions a grappler may take (PHB p.156); the
        // same reading as Lay on Hands (LayOnHandsRules.CanUse; delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1).
        if (actor.IsGrappling())
        {
            reason = "Grappling (not a grapple action, PHB p.156)";
            return false;
        }

        if (GetRemainingTurnUndeadAttempts(actor) <= 0)
        {
            reason = "No attempts left";
            return false;
        }

        reason = "Ready";
        return true;
    }

    internal static bool IsUndeadCharacter(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return false;

        if (string.Equals(character.Stats.CreatureType, "Undead", StringComparison.OrdinalIgnoreCase))
            return true;

        if (character.Stats.CreatureTags != null)
        {
            for (int i = 0; i < character.Stats.CreatureTags.Count; i++)
            {
                if (string.Equals(character.Stats.CreatureTags[i], "Undead", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
    /// <summary>Turning reaches 60 ft (PHB p.159): 12 squares, counted as every distance on the grid is (PHB p.147).</summary>
    internal const int TurnUndeadRangeSquares = 12;

    /// <summary>
    /// The living undead other than <paramref name="turner"/> within <paramref name="maxRangeSquares"/> squares, whatever
    /// their side (PHB p.159 turns "the closest turnable undead" with no exemption for the turner's allies; explicit RAW,
    /// 2026-10-30), measured as every distance on the grid is (the second diagonal counts double, PHB p.147), closest
    /// first: ties to the lower Hit
    /// Dice, which turns the most (D9, delegated RAW reading 2026-10-29), then by name so the order is stable. Line of
    /// effect is not tested here (<see cref="GetTurnUndeadCandidates"/>).
    /// </summary>
    private List<CharacterController> GetTurnableUndeadInRange(CharacterController turner, int maxRangeSquares = TurnUndeadRangeSquares)
    {
        var result = new List<CharacterController>();
        if (turner == null || turner.Stats == null)
            return result;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == turner || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsUndeadCharacter(candidate))
                continue;

            if (TurnUndeadDistance(turner, candidate) <= maxRangeSquares)
                result.Add(candidate);
        }

        result.Sort((a, b) =>
        {
            int byDist = TurnUndeadDistance(turner, a).CompareTo(TurnUndeadDistance(turner, b));
            if (byDist != 0) return byDist;

            int byHd = a.Stats.GetHitDice().CompareTo(b.Stats.GetHitDice());
            if (byHd != 0) return byHd;

            return string.Compare(a.Stats.CharacterName ?? string.Empty, b.Stats.CharacterName ?? string.Empty, StringComparison.Ordinal);
        });

        return result;
    }

    /// <summary>The distance in squares from the turner's space to the undead's nearest square (PHB p.147: 5-10-5 diagonals).</summary>
    private static int TurnUndeadDistance(CharacterController turner, CharacterController undead)
        => turner.GetMinimumDistanceToTarget(undead, chebyshev: false);

    /// <summary>
    /// The undead <paramref name="turner"/> may turn now, in the order the turning takes them (D9, PHB p.159): the undead
    /// within 60 ft, of any side (<see cref="GetTurnableUndeadInRange"/>: closest first, ties to the lower Hit Dice) that the
    /// turner has line of effect to (AoESystem.HasLineOfEffectToSpace: some square of the undead's space is reached from
    /// the turner's space; a creature behind total cover has none, and the walls that block line of effect are the only
    /// total cover the grid has, GRID-009). <paramref name="blocked"/> gets the undead in range that have no line of effect
    /// (CMB-026). Pure: no dice, no log.
    /// </summary>
    internal List<CharacterController> GetTurnUndeadCandidates(CharacterController turner, out List<CharacterController> blocked)
    {
        blocked = new List<CharacterController>();
        var result = new List<CharacterController>();
        if (turner == null || turner.Stats == null)
            return result;

        foreach (CharacterController undead in GetTurnableUndeadInRange(turner, TurnUndeadRangeSquares))
        {
            if (AoESystem.HasLineOfEffectToSpace(turner.GridPosition, turner.GetCurrentSpaceSquares(), undead.GridPosition, Mathf.Max(1, undead.GetCurrentSpaceSquares())))
                result.Add(undead);
            else
                blocked.Add(undead);
        }
        return result;
    }

    /// <summary>The turner's turning level: cleric level, plus paladin level - 3 from paladin 4th, plus Improved Turning.</summary>
    internal static int GetTurnUndeadEffectiveLevel(CharacterController turner)
    {
        if (turner == null || turner.Stats == null)
            return 0;

        // Cleric level, plus paladin level - 3 from paladin 4th level; a cleric/paladin adds the two (PHB p.44, p.59;
        // CharacterStats.TurnUndeadEffectiveLevel, CHR-020).
        int effectiveLevel = turner.Stats.TurnUndeadEffectiveLevel;
        if (effectiveLevel <= 0)
            return 0;

        // Improved Turning feat (PHB p.96): +1 effective cleric level for turning
        effectiveLevel += FeatManager.GetImprovedTurningLevelBonus(turner.Stats);

        return effectiveLevel;
    }

    /// <summary>Ranks in Knowledge (religion) that give the +2 on turning checks against undead (PHB p.33, p.78, p.159).</summary>
    internal const int KnowledgeReligionRanksForTurningBonus = 5;

    /// <summary>
    /// The turning check's bonus against undead beyond Charisma: +2 with 5 or more ranks in Knowledge (religion) (PHB p.33,
    /// p.78 synergy, p.159). Only for turning undead: the Plant and elemental domains' turning does not take it.
    /// </summary>
    internal static int TurningCheckBonusAgainstUndead(CharacterController turner)
    {
        if (turner == null || turner.Stats == null || turner.Stats.Skills == null)
            return 0;
        return turner.Stats.Skills.TryGetValue("Knowledge (Religion)", out Skill k) && k != null && k.Ranks >= KnowledgeReligionRanksForTurningBonus ? 2 : 0;
    }

    /// <summary>The Hit Dice of the most powerful undead a turning check of <paramref name="turningCheckTotal"/> affects (PHB p.159 Table 8-9).</summary>
    internal static int ComputeTurnUndeadMaxHitDice(int turningCheckTotal, int clericLevel)
    {
        int level = Mathf.Max(1, clericLevel);

        if (turningCheckTotal <= 0) return Mathf.Max(0, level - 4);
        if (turningCheckTotal <= 3) return Mathf.Max(0, level - 3);
        if (turningCheckTotal <= 6) return Mathf.Max(0, level - 2);
        if (turningCheckTotal <= 9) return Mathf.Max(0, level - 1);
        if (turningCheckTotal <= 12) return level;
        if (turningCheckTotal <= 15) return level + 1;
        if (turningCheckTotal <= 18) return level + 2;
        if (turningCheckTotal <= 21) return level + 3;
        return level + 4;
    }

    /// <summary>The bar of a CanUseTurnUndead refusal: the spent standard action, the grapple, the daily uses, else the class.</summary>
    internal static LegalityBar TurnUndeadRefusalBar(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return LegalityBar.Other;
        if (reason.StartsWith("Standard action", StringComparison.Ordinal))
            return LegalityBar.Action;
        if (reason.StartsWith("Grappling", StringComparison.Ordinal))
            return LegalityBar.Condition;
        if (reason.StartsWith("No attempts", StringComparison.Ordinal))
            return LegalityBar.Resource;
        return LegalityBar.Other;
    }

    // ── The AI's choice (design choice C6) ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The AI's turn undead (design choice C6, AI-054): when its profile chooses to turn (AIProfile.ShouldTurnUndead, by
    /// default a hostile turnable undead within 60 ft with line of effect that its best turning check could affect), the
    /// creature turns through the catalog's turn-undead entry, the executor the PC's Turn Undead button runs; the AI skips
    /// undead that are already turned (D9). True when it turned.
    /// </summary>
    public bool TryNPCTurnUndeadTurnForAI(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || !npc.Stats.CanEverTurnUndead)
            return false;
        bool wants = npc.aiProfile != null ? npc.aiProfile.ShouldTurnUndead(npc) : DND35.AI.AIProfile.DefaultShouldTurnUndead(npc);
        if (!wants)
            return false;
        return RunAIActionNow(ActionContext.For(ActionIds.TurnUndead, npc, ActorSource.AI)).Performed;
    }

    // ── The PC's Turn Undead button: the range preview, the click that invokes, the skip question ─────────────────

    public void OnTurnUndeadButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        if (!CanUseTurnUndead(pc, out string reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} cannot use Turn Undead: {reason}."));
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        EnterTurnUndeadTargeting(pc);
    }

    /// <summary>
    /// The PC's range preview before a turning (the Turn Undead button, or the Sun domain's greater turning with
    /// <paramref name="greaterTurning"/>); any click invokes it (<see cref="ConfirmTurnUndeadTargeting"/>), right-click or
    /// Esc cancels with nothing spent.
    /// </summary>
    private void EnterTurnUndeadTargeting(CharacterController turner, bool greaterTurning = false)
    {
        if (turner == null)
        {
            ShowActionChoices();
            return;
        }

        _isSelectingTurnUndead = true;
        _turnUndeadPendingInvoker = turner;
        _turnUndeadPendingGreater = greaterTurning;
        _isSelectingSpecialAttack = false;
        CurrentSubPhase = PlayerSubPhase.ConfirmingTurnUndead;

        CombatUI?.HideSpecialAttackMenu();
        CombatUI?.SetActionButtonsVisible(false);

        ShowTurnUndeadTargetingPreview(turner);

        int inRangeUndead = GetTurnUndeadCandidates(turner, out _).Count;
        string what = greaterTurning ? "GREATER TURNING" : "TURN UNDEAD";
        CombatUI?.SetTurnIndicator($"{what}: {inRangeUndead} undead in 60 ft with line of effect, closest first. Left-click any cell to invoke, Right-click/Esc to cancel.");
    }

    private void ShowTurnUndeadTargetingPreview(CharacterController turner)
    {
        if (turner == null)
            return;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        int sizePadding = Mathf.Max(0, turner.GetVisualSquaresOccupied() - 1);
        List<SquareCell> rangeCells = GetCellsInChebyshevRange(turner.GridPosition, TurnUndeadRangeSquares + sizePadding);
        for (int i = 0; i < rangeCells.Count; i++)
        {
            SquareCell cell = rangeCells[i];
            if (cell == null)
                continue;
            // 60 ft as the grid counts it (PHB p.147), not the Chebyshev box.
            if (SquareGridUtils.GetDistance(turner.GridPosition, cell.Coords) > TurnUndeadRangeSquares + sizePadding)
                continue;

            cell.SetHighlight(HighlightType.SpellRange);
            _highlightedCells.Add(cell);
        }

        List<CharacterController> undeadInRange = GetTurnUndeadCandidates(turner, out _);
        for (int i = 0; i < undeadInRange.Count; i++)
            HighlightCharacterFootprint(undeadInRange[i], HighlightType.Attack, addToSelectableCells: true);

        HighlightCharacterFootprint(turner, HighlightType.Selected, addToSelectableCells: true);
    }

    /// <summary>The skip question's answers (the harness clicks them by these labels).</summary>
    internal const string TurnUndeadSkipOption = "Skip already turned undead";
    internal const string TurnUndeadIncludeOption = "Include already turned undead";

    /// <summary>
    /// The click that invokes the PC's turning. When undead in range are already turned, the player is asked once whether
    /// to skip them (PHB p.159: the turner may skip already turned undead; D9); a cancel there spends nothing. The turning
    /// itself is the catalog's turn-undead entry (or, for a greater turning, the domain-power entry's Sun variant) through
    /// RunPlayerAction: the executor the AI runs too.
    /// </summary>
    private void ConfirmTurnUndeadTargeting()
    {
        if (!_isSelectingTurnUndead)
            return;

        CharacterController turner = _turnUndeadPendingInvoker;
        bool greater = _turnUndeadPendingGreater;
        _isSelectingTurnUndead = false;
        _turnUndeadPendingInvoker = null;
        _turnUndeadPendingGreater = false;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI?.SetActionButtonsVisible(true);

        if (turner == null || turner.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        // The preview is over: the PC is busy until the turning's follow-up (or a cancel) returns the action panel.
        CurrentSubPhase = PlayerSubPhase.Animating;

        var alreadyTurned = new List<CharacterController>();
        foreach (CharacterController undead in GetTurnUndeadCandidates(turner, out _))
            if (IsTurnedUndead(undead))
                alreadyTurned.Add(undead);

        if (alreadyTurned.Count == 0 || CombatUI == null)
        {
            RunPlayerTurnUndead(turner, greater, includeTurned: false);
            return;
        }

        var names = new List<string>();
        foreach (CharacterController undead in alreadyTurned)
            names.Add($"{undead.Stats.CharacterName} ({GetTurnedRoundsRemaining(undead)} rds left)");
        CombatUI.ShowPickUpItemSelection(
            actorName: turner.Stats.CharacterName,
            itemOptions: new List<string> { TurnUndeadSkipOption, TurnUndeadIncludeOption },
            onSelect: index => RunPlayerTurnUndead(turner, greater, includeTurned: index == 1),
            onCancel: () =>
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Turn Undead cancelled."));
                ShowActionChoices();
            },
            titleOverride: greater ? "Greater Turning" : "Turn Undead",
            bodyOverride: "Already turned within 60 ft: " + string.Join(", ", names)
                          + ". Skip them (PHB p.159) or let the turning take them in their order?");
    }

    private void RunPlayerTurnUndead(CharacterController turner, bool greater, bool includeTurned)
    {
        ActionContext ctx = greater
            ? ActionContext.For(ActionIds.DomainPower, turner, ActorSource.Ui)
            : ActionContext.For(ActionIds.TurnUndead, turner, ActorSource.Ui);
        if (greater)
            ctx.Variant = DomainPowerActionExecutor.SunVariant;
        ctx.CoreRequest = new TurnUndeadRequest { IncludeTurned = includeTurned };
        RunPlayerAction(ctx);
    }

    private void CancelTurnUndeadTargeting()
    {
        if (!_isSelectingTurnUndead && CurrentSubPhase != PlayerSubPhase.ConfirmingTurnUndead)
            return;

        _isSelectingTurnUndead = false;
        _turnUndeadPendingInvoker = null;
        _turnUndeadPendingGreater = false;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Turn Undead cancelled."));
        ShowActionChoices();
    }
}
