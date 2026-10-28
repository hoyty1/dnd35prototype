using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// GameManager partial class: the deciders' side of the cast core (batch 11 step 7, docs/designs/cast_core_batch11.md).
///
/// <see cref="UiCastDecider"/> calls the Ui methods here: the PC's choice dialogs, the AoO prompt and the UI follow-up of
/// a finished cast. This is the only cast-core code that touches CombatUI beyond the log, the sub-phase, the highlights,
/// the stat panels, ShowActionChoices and AfterAttackDelay; the stages themselves (GameManager.CastCore.cs) never do.
/// <see cref="AiCastDecider"/> calls <see cref="AiResolveCastProvocation"/> and <see cref="AiOnCastFinished"/> (the NPC
/// path runs through the core since step 8: TryNPCPerformSpellCast, RunCastSync).
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The PC's choices for the cast: the Resist Energy, Protection from Energy and Fire Shield dialogs (which re-enter
    /// the cast through BeginPendingSpellTargeting once answered; the energy dialogs at a hold of the charge resume the
    /// hold through ResumeCastAfterChoiceDialog instead, batch 11 step 15), then the Magic Weapon, Keen Edge and Greater Magic
    /// Weapon pickers (which re-enter through PerformSpellCast). A picker with nothing to pick refuses the cast.
    /// </summary>
    internal CastChoiceFill UiFillCastChoices(CastContext ctx)
    {
        SpellCastRequest req = ctx.Request;
        CharacterController caster = req.Caster;
        CharacterController target = req.Aim.Target;
        string spellId = req.Spell.SpellId;

        if (string.Equals(spellId, SpellNames.RESIST_ENERGY, StringComparison.Ordinal) && !req.Choices.Energy.HasValue)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Resist Energy requires selecting an energy type before casting."));
            ShowResistEnergyTypeSelection(caster);
            return CastChoiceFill.Suspended;
        }

        if (string.Equals(spellId, SpellNames.PROTECTION_FROM_ENERGY, StringComparison.Ordinal) && !req.Choices.Energy.HasValue)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Protection from Energy requires selecting an energy type before casting."));
            ShowProtectionFromEnergyTypeSelection(caster);
            return CastChoiceFill.Suspended;
        }

        if (string.Equals(spellId, SpellNames.FIRE_SHIELD, StringComparison.Ordinal) && !req.Choices.FireShieldWarm.HasValue)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Fire Shield requires selecting a shield type before casting."));
            ShowFireShieldTypeSelection(caster);
            return CastChoiceFill.Suspended;
        }

        // Dimension Door's destination (PHB p.221; batch 11 step 27, SPL-091): the escape square the game picks for the
        // player, as before (ChooseDimensionDoorDestination; a square picker is UI-053). The AI decider fills it the same way.
        if (string.Equals(spellId, SpellNames.DIMENSION_DOOR, StringComparison.Ordinal) && !req.Choices.Destination.HasValue)
            req.Choices.Destination = ChooseDimensionDoorDestination(caster, req.Spell);

        CastChoiceFill fill = TryHandleMagicWeaponWeaponSelection(caster, target);
        if (fill != CastChoiceFill.Filled)
            return fill;

        fill = TryHandleKeenEdgeWeaponSelection(caster, target);
        if (fill != CastChoiceFill.Filled)
            return fill;

        return TryHandleGreaterMagicWeaponSelection(caster, target);
    }

    /// <summary>
    /// The PC's choice for a melee touch spell (batch 11 step 10; PHB p.141, p.176): the touch prompt's Cast Now,
    /// Discharge Later and Cancel buttons (CombatUI.ShowTouchSpellPrompt). Without a CombatUI nothing is asked and the
    /// cast stays open, as before.
    /// </summary>
    internal void UiChooseTouchDelivery(CastContext ctx, Action<CastTouchDelivery> answer)
    {
        CombatUI?.ShowTouchSpellPrompt(
            ctx.Request.Spell,
            onCastNow: () => answer(CastTouchDelivery.CastNow),
            onDischargeLater: () => answer(CastTouchDelivery.HoldCharge),
            onCancel: () => answer(CastTouchDelivery.Cancel));
    }

    /// <summary>
    /// The PC's answer to a threatened cast: the AoO prompt (ShowAoOActionConfirmation, through
    /// ResolveSpellcastProvocation), whose Cast Defensively and Proceed buttons end the caster's concentration
    /// (EndConcentrationForCasting, decision D1) and resolve the rolls through ResolveThreatenedSpellcast and whose Cancel
    /// backs out.
    /// </summary>
    internal void UiChooseCastProvocation(CastContext ctx, Action<CastProvocationAnswer> answer)
    {
        ResolveSpellcastProvocation(ctx.Request.Caster, ctx.Request.Spell, false, () => EndConcentrationForCasting(ctx), canProceed =>
            answer(canProceed
                ? CastProvocationAnswer.Proceeds
                : _spellcastProvocationCancelled ? CastProvocationAnswer.Cancelled : CastProvocationAnswer.Lost));
    }

    /// <summary>
    /// The UI follow-up of a resolved PC cast that comes before its combat-end check, in the old PerformSpellCast order:
    /// the result log becomes the last combat log, the stat panels refresh (UpdateAllStatsUI, which also applies and
    /// removes the Flanked conditions) and the highlights clear.
    /// </summary>
    internal void UiBeforeCastCombatEndCheck(CastContext ctx)
    {
        if (ctx.ResultLog != null)
            _lastCombatLog = ctx.ResultLog;
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();
    }

    /// <summary>
    /// The UI follow-up of a finished PC cast: a refusal, a cancel, a failed spend or an outcome that asks for it
    /// (<see cref="CastOutcome.ShowsActionMenu"/>: a summon that spawned nothing) returns to the action menu; any
    /// other cast refreshes the stat panels, clears the highlights and keeps its result log as the last combat log (a
    /// cast resolved at the CombatEnd stage did that before its combat-end check, UiBeforeCastCombatEndCheck) and,
    /// unless it ended the combat, pauses before the next action (AfterAttackDelay: 1.5 s after a resolved cast, 1 s
    /// after a lost, failed or mirrored one).
    /// </summary>
    internal void UiOnCastFinished(CastContext ctx, CastOutcome outcome)
    {
        CharacterController caster = ctx.Request.Caster;

        if (outcome.Kind == CastOutcomeKind.Cancelled)
        {
            Grid.ClearAllHighlights();
            UpdateAllStatsUI();
            ShowActionChoices();
            return;
        }

        if (outcome.BackToActionMenu || outcome.ShowsActionMenu)
        {
            ShowActionChoices();
            return;
        }

        bool resolvedAtCombatEnd = outcome.Kind == CastOutcomeKind.Resolved && outcome.At == CastStage.CombatEnd;
        if (!resolvedAtCombatEnd)
        {
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();
            if (ctx.ResultLog != null)
                _lastCombatLog = ctx.ResultLog;
        }

        if (outcome.CombatEnded)
            return;

        float delay = resolvedAtCombatEnd ? 1.5f : 1.0f;
        if (caster != null)
            StartCoroutine(AfterAttackDelay(caster, delay));
        else
            ShowActionChoices();
    }

    /// <summary>
    /// The NPC decision and rolls for a threatened cast (AiCastDecider.ChooseProvocation): the caster's concentration
    /// ends before the rolls (EndConcentrationForCasting, decision D1), as for a PC.
    /// </summary>
    internal bool AiResolveCastProvocation(CastContext ctx)
    {
        return ResolveNPCSpellcastProvocation(ctx.Request.Caster, ctx.Request.Spell, () => EndConcentrationForCasting(ctx));
    }

    /// <summary>
    /// The follow-up of a finished NPC cast (AiCastDecider.OnCastFinished), as the old NPC executor did it before it
    /// returned true: a cast that counts as cast (<see cref="CastOutcome.WasCast"/>) keeps its result log, if it has one
    /// (a resolved cast or a struck mirror image), as the last combat log and refreshes the stat panels, which also
    /// applies and removes the Flanked conditions. A refused or unspendable cast (the old false returns) does neither.
    /// The cast core has run the combat-end check before this (batch 12 step 9, CORE-039).
    /// </summary>
    internal void AiOnCastFinished(CastContext ctx, CastOutcome outcome)
    {
        if (outcome == null || !outcome.WasCast)
            return;
        if (ctx.ResultLog != null)
            _lastCombatLog = ctx.ResultLog;
        UpdateAllStatsUI();
    }

    /// <summary>
    /// The AI decider's choices that read the battlefield (AiCastDecider.FillChoices; batch 11 step 27, SPL-091):
    /// Dimension Door's destination (<see cref="ChooseDimensionDoorDestination"/>, the escape square the UI decider gives
    /// the player too) and a Summon Monster's or Summon Nature's Ally's creature and square
    /// (<see cref="AiFillSummonChoices"/>). Refused when a summon finds no creature or no square; nothing is spent then.
    /// </summary>
    internal CastChoiceFill AiFillCasterChoices(SpellCastRequest req)
    {
        if (req == null || req.Spell == null || req.Choices == null)
            return CastChoiceFill.Filled;
        if (string.Equals(req.Spell.SpellId, SpellNames.DIMENSION_DOOR, StringComparison.Ordinal) && !req.Choices.Destination.HasValue)
            req.Choices.Destination = ChooseDimensionDoorDestination(req.Caster, req.Spell);
        if (req.ResolveKind == CastResolveKind.Summon && IsSummonMonsterSpell(req.Spell))
            return AiFillSummonChoices(req);
        return CastChoiceFill.Filled;
    }

    /// <summary>
    /// The AI's summon choices (batch 11 step 27, SPL-091), the ones the PC's prompts and placement click make: the list
    /// of the spell's own level (one creature, PHB p.285; the count rule and its roll are the Summon resolve's,
    /// RollSummonCreatureCount), the creature most useful to the caster's side on it
    /// (AiCastChoiceHeuristics.ChooseSummonOption), and the square it appears on (<see cref="FindAiSummonSquare"/>). A
    /// choice the caller already made is kept. Refused with no creature or no square.
    /// </summary>
    internal CastChoiceFill AiFillSummonChoices(SpellCastRequest req)
    {
        CastChoices choices = req.Choices;
        CharacterController caster = req.Caster;
        int spellLevel = SummonMonsterLists.GetSummonMonsterSpellLevel(req.Spell.SpellId);
        if (choices.SummonListLevel <= 0)
            choices.SummonListLevel = spellLevel;
        if (choices.SummonSelection == null)
            choices.SummonSelection = AiCastChoiceHeuristics.ChooseSummonOption(GetSummonOptionsForSpell(req.Spell, caster, choices.SummonListLevel));
        if (choices.SummonSelection == null)
            return CastChoiceFill.Refused;
        if (choices.SummonCountInfo == null)
            choices.SummonCountInfo = SummonMonsterLists.GetCreatureCountInfo(spellLevel, choices.SummonListLevel);
        if (req.Aim == null)
            req.Aim = new CastAim();
        if (!req.Aim.Destination.HasValue)
            req.Aim.Destination = FindAiSummonSquare(caster, choices.SummonSelection, Mathf.Max(1, CasterLevelRules.RangeSquares(req)));
        return req.Aim.Destination.HasValue ? CastChoiceFill.Filled : CastChoiceFill.Refused;
    }

    /// <summary>
    /// The square an AI summon puts its creature on (batch 11 step 27): the free square for the creature's size nearest
    /// the caster's nearest living foe (team allegiance), within <paramref name="rangeSquares"/> of the caster (the PC
    /// placement highlight's measure, ShowSummonPlacementTargets), so the creature can attack in the turn it appears
    /// (FindBestAdditionalSummonCell, the search the Summon resolve uses for the extra creatures). With no foe, the free
    /// square nearest the caster. Null when no square in range has room.
    /// </summary>
    internal Vector2Int? FindAiSummonSquare(CharacterController caster, SummonMonsterOption option, int rangeSquares)
    {
        if (caster == null || option == null || Grid == null)
            return null;
        NPCDefinition def = NPCDatabase.Get(option.NpcDefinitionId);
        if (def == null)
            return null;
        CharacterController nearestFoe = null;
        int nearest = int.MaxValue;
        foreach (CharacterController c in GetAllCharacters())
        {
            if (c == null || c == caster || c.Stats == null || c.Stats.IsDead || !TeamUtility.IsEnemy(caster, c))
                continue;
            int d = CasterLevelRules.DistanceToTarget(caster, c);
            if (d < nearest)
            {
                nearest = d;
                nearestFoe = c;
            }
        }
        Vector2Int preferred = nearestFoe != null ? nearestFoe.GridPosition : caster.GridPosition;
        SquareCell cell = FindBestAdditionalSummonCell(caster, preferred, def.SizeCategory.GetSpaceWidthSquares(), rangeSquares);
        return cell != null ? cell.Coords : (Vector2Int?)null;
    }

    /// <summary>
    /// Where <paramref name="caster"/> can move with one move action this turn to touch <paramref name="target"/> (batch 11
    /// step 27, SPL-115; AiCastDecider.TouchDeliveryFor and the AI's held-charge delivery): a free square within its
    /// current move range, reached by a path (FindPath, no threat avoidance, as the AI's movement search,
    /// AIService.EvaluateMovementOptions), from which the target's nearest square is adjacent (the touch's reach,
    /// CasterLevelRules.InRange); the one with the shortest path, then the fewest attacks of opportunity. Null without a
    /// move action or such a square.
    /// </summary>
    internal Vector2Int? FindTouchApproachSquareForAI(CharacterController caster, CharacterController target)
    {
        if (caster == null || target == null || caster.Stats == null || Grid == null || caster.Actions == null || !caster.Actions.HasMoveAction)
            return null;
        if (ThreatSystem.IsMoverIncapacitated(caster))
            return null;
        int moveRange = GetCurrentMoveRangeSquares(caster);
        if (moveRange <= 0)
            return null;
        int size = caster.GetVisualSquaresOccupied();
        List<Vector2Int> targetSquares = target.GetOccupiedSquares();
        Vector2Int? best = null;
        int bestLength = int.MaxValue;
        int bestAoOs = int.MaxValue;
        foreach (SquareCell cell in Grid.GetCellsInRange(caster.GridPosition, moveRange))
        {
            if (cell == null || cell.Coords == caster.GridPosition)
                continue;
            int reach = int.MaxValue;
            if (targetSquares != null && targetSquares.Count > 0)
            {
                for (int i = 0; i < targetSquares.Count; i++)
                    reach = Mathf.Min(reach, SquareGridUtils.GetDistance(cell.Coords, targetSquares[i]));
            }
            else
            {
                reach = SquareGridUtils.GetDistance(cell.Coords, target.GridPosition);
            }
            if (reach > 1)
                continue;
            if (!Grid.CanPlaceCreature(cell.Coords, size, caster))
                continue;
            AoOPathResult path = FindPath(caster, cell.Coords, avoidThreats: false, maxRangeOverride: moveRange);
            if (path == null || path.Path == null || path.Path.Count == 0)
                continue;
            int aoos = path.ProvokedAoOs != null ? path.ProvokedAoOs.Count : 0;
            if (path.Path.Count < bestLength || (path.Path.Count == bestLength && aoos < bestAoOs))
            {
                best = cell.Coords;
                bestLength = path.Path.Count;
                bestAoOs = aoos;
            }
        }
        return best;
    }
}
