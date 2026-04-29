using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DND35.AI.Profiles;

public class GrappleSystem : BaseCombatManeuver
{
}

public partial class GameManager
{
    private bool _isGrappleMoveSelection;
    private CharacterController _grappleMoveActor;
    private int _grappleMoveMaxRangeSquares;
    private readonly List<CharacterController> _grappleMoveOpponents = new List<CharacterController>();
    private readonly Dictionary<Vector2Int, List<Vector2Int>> _grappleMovePathsByDestination = new Dictionary<Vector2Int, List<Vector2Int>>();

    // Active grapple context menu lock (pauses token alternation while contextual menu is open).
    private CharacterController _grappleContextMenuLockOwner;
    // Free adjacent reposition after ending a grapple (escape/release).
    private bool _isFreeAdjacentGrappleMoveSelection;
    private CharacterController _freeAdjacentGrappleMoveActor;
    private readonly List<Vector2Int> _freeAdjacentGrappleMoveDestinations = new List<Vector2Int>();

    private bool TryGetPinnedGrappleOpponent(CharacterController actor, out CharacterController opponent)
    {
        opponent = null;
        if (actor == null || actor.Stats == null)
            return false;

        if (!actor.HasCondition(CombatConditionType.Pinned))
            return false;

        if (!actor.TryGetGrappleState(out CharacterController liveOpponent, out _, out _, out _))
            return false;

        if (liveOpponent == null || liveOpponent.Stats == null || liveOpponent.Stats.IsDead)
            return false;

        opponent = liveOpponent;
        return true;
    }

    private bool RedirectPinnedCharacterToGrappleMenu(CharacterController actor, string attemptedActionLabel)
    {
        if (!TryGetPinnedGrappleOpponent(actor, out CharacterController opponent))
            return false;

        string actionName = string.IsNullOrWhiteSpace(attemptedActionLabel) ? "that action" : attemptedActionLabel;
        CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} is pinned and cannot use {actionName}. Only grapple escape actions are allowed.");

        if (!actor.Actions.HasStandardAction)
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} has no standard action left to attempt an escape and must end the turn.");

        // Grapple actions are now presented as direct action buttons in the main action panel.
        ShowActionChoices();

        return true;
    }

    public void ShowGrappleActionMenu()
    {
        CharacterController pc = ActivePC;
        LogMenuFlow("ShowGrappleActionMenu(PUBLIC):ENTER", pc);

        if (pc == null)
        {
            LogMenuFlow("ShowGrappleActionMenu(PUBLIC):NO_ACTIVE_PC", pc);
            ShowActionChoices();
            return;
        }

        if (!pc.IsGrappling())
        {
            LogMenuFlow("ShowGrappleActionMenu(PUBLIC):PC_NOT_GRAPPLING", pc);
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is not currently grappling.");
            ShowActionChoices();
            return;
        }

        if (!pc.TryGetGrappleState(out CharacterController opponent, out _, out _, out _))
        {
            LogMenuFlow("ShowGrappleActionMenu(PUBLIC):NO_GRAPPLE_STATE", pc);
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is no longer in a grapple.");
            ShowActionChoices();
            return;
        }

        ShowGrappleActionMenu(pc, opponent);
    }

    public bool CanUseGrappleAction(CharacterController character, GrappleActionType action)
    {
        if (character == null)
            return false;

        if (character.IsPinningOpponent())
        {
            return action == GrappleActionType.DamageOpponent
                || action == GrappleActionType.UseOpponentWeapon
                || action == GrappleActionType.MoveHalfSpeed
                || action == GrappleActionType.DisarmSmallObject
                || action == GrappleActionType.ReleasePinnedOpponent;
        }

        if (character.IsPinned())
        {
            return action == GrappleActionType.OpposedGrappleEscape
                || action == GrappleActionType.EscapeArtist;
        }

        return true;
    }

    public List<GrappleActionType> GetAvailableGrappleActions(CharacterController character)
    {
        var actions = new List<GrappleActionType>();
        if (character == null)
            return actions;

        if (character.IsPinningOpponent())
        {
            actions.Add(GrappleActionType.DamageOpponent);
            actions.Add(GrappleActionType.UseOpponentWeapon);
            actions.Add(GrappleActionType.MoveHalfSpeed);
            actions.Add(GrappleActionType.DisarmSmallObject);
            actions.Add(GrappleActionType.ReleasePinnedOpponent);
            return actions;
        }

        if (character.IsPinned())
        {
            actions.Add(GrappleActionType.OpposedGrappleEscape);
            actions.Add(GrappleActionType.EscapeArtist);
            return actions;
        }

        actions.Add(GrappleActionType.DamageOpponent);
        actions.Add(GrappleActionType.AttackWithLightWeapon);
        actions.Add(GrappleActionType.AttackUnarmed);
        actions.Add(GrappleActionType.PinOpponent);
        actions.Add(GrappleActionType.BreakPin);
        actions.Add(GrappleActionType.MoveHalfSpeed);
        actions.Add(GrappleActionType.UseOpponentWeapon);
        actions.Add(GrappleActionType.OpposedGrappleEscape);
        actions.Add(GrappleActionType.EscapeArtist);
        actions.Add(GrappleActionType.DrawLightWeapon);
        actions.Add(GrappleActionType.RetrieveSpellComponent);
        actions.Add(GrappleActionType.ReleasePinnedOpponent);
        return actions;
    }

    private bool TryHandleDirectGrappleAction(GrappleActionType actionType)
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null)
        {
            ShowActionChoices();
            return false;
        }

        if (!pc.IsGrappling() || !pc.TryGetGrappleState(out _, out _, out _, out _))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is not currently in a valid grapple state.");
            ShowActionChoices();
            return false;
        }

        if (!CanUseGrappleAction(pc, actionType))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot use {actionType} in the current pin state.");
            ShowActionChoices();
            return false;
        }

        if (CharacterController.IsIterativeGrappleAttackAction(actionType) && !CanUseGrappleAttackOption(pc))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no grapple attacks remaining in the shared attack pool this turn.");
            ShowActionChoices();
            return false;
        }
        if (actionType == GrappleActionType.DamageOpponent)
        {
            ShowGrappleDamageModeMenu(pc);
            return true;
        }

        if (actionType == GrappleActionType.UseOpponentWeapon)
        {
            ShowUseOpponentWeaponHandSelectionMenu(pc);
            return true;
        }

        ExecuteGrappleAction(pc, actionType);
        return true;
    }

    public void OnGrappleDamageButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.DamageOpponent);
    public void OnGrappleLightWeaponAttackButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.AttackWithLightWeapon);
    public void OnGrappleUnarmedAttackButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.AttackUnarmed);

    public void OnGrapplePinButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.PinOpponent);
    public void OnGrappleBreakPinButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.BreakPin);
    public void OnGrappleEscapeArtistButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc != null && TryHandleAnimateRopeEscapeAction(pc, consumeStandardAction: true))
        {
            ShowActionChoices();
            return;
        }

        TryHandleDirectGrappleAction(GrappleActionType.EscapeArtist);
    }
    public void OnGrappleEscapeCheckButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.OpposedGrappleEscape);
    public void OnGrappleMoveButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.MoveHalfSpeed);
    public void OnGrappleUseOpponentWeaponButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.UseOpponentWeapon);
    public void OnGrappleDisarmSmallObjectButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.DisarmSmallObject);
    public void OnGrappleReleasePinButtonPressed() => TryHandleDirectGrappleAction(GrappleActionType.ReleasePinnedOpponent);

    public void OnOverrunButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        Debug.Log($"[Overrun][UI] Overrun button pressed by {pc.Stats.CharacterName}. Forwarding to destination-based flow.");

        if (!CanUseOverrun(pc, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot use Overrun: {reason}.");
            return;
        }

        if (RedirectPinnedCharacterToGrappleMenu(pc, "overrun"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "overrun"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        OnSpecialAttackSelected(SpecialAttackType.Overrun, false);
    }

    private bool TryGetGrappledOrPinnedState(CharacterController caster, out string conditionLabel)
    {
        conditionLabel = null;
        if (caster == null)
            return false;

        bool isPinned = caster.HasCondition(CombatConditionType.Pinned);
        bool isGrappled = caster.HasCondition(CombatConditionType.Grappled);

        if (!isPinned && !isGrappled)
            return false;

        conditionLabel = isPinned ? "pinned" : "grappled";
        return true;
    }

    private bool CanBeginSpellcastWhileGrappledOrPinned(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null)
            return false;

        if (!TryGetGrappledOrPinnedState(caster, out string conditionLabel))
            return true;

        // D&D 3.5e: While grappled/pinned, you can only cast spells with casting time
        // of 1 standard action or less.
        bool hasAllowedCastingTime = spell.ActionType == SpellActionType.Standard
            || spell.ActionType == SpellActionType.Swift
            || spell.ActionType == SpellActionType.Free;
        if (!hasAllowedCastingTime)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} cannot cast {spell.Name} while {conditionLabel}: casting time must be 1 standard action or less.");
            return false;
        }

        bool isPinned = caster.HasCondition(CombatConditionType.Pinned);
        if (isPinned && spell.HasVerbalComponent)
        {
            CombatUI?.ShowCombatLog("⚠ Cannot cast - pinned (no verbal components)");
            return false;
        }

        // Grappled/pinned casters also cannot provide somatic components.
        if (spell.HasSomaticComponent)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} cannot cast {spell.Name} while {conditionLabel}: spells with somatic components are blocked.");
            return false;
        }

        return true;
    }

    private bool ResolveGrappledOrPinnedCastingConcentration(
        CharacterController caster,
        SpellcastingComponent spellComp,
        SpellData spell,
        MetamagicData metamagic,
        bool hasMetamagicApplied,
        int slotLevelToConsume,
        bool isSpontaneous,
        int spontaneousLevel,
        string spontaneousSacrificedSpellId)
    {
        if (caster == null || spell == null)
            return false;

        if (!TryGetGrappledOrPinnedState(caster, out string conditionLabel))
            return true;

        int dc = 20 + spell.SpellLevel;
        ConcentrationCheckResult check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
            caster,
            dc,
            ConcentrationCheckType.Grappled,
            spell);

        CombatUI?.ShowCombatLog(
            $"🪢 {conditionLabel.ToUpperInvariant()} casting concentration ({caster.Stats.CharacterName}, {spell.Name}): d20 {check.D20Roll} + conc {check.TotalBonus} = {check.TotalRoll} vs DC {dc} (20 + spell level {spell.SpellLevel})");

        if (check.Success)
            return true;

        bool consumed = ConsumePendingSpellSlot(
            spellComp,
            spell,
            metamagic,
            hasMetamagicApplied,
            slotLevelToConsume,
            isSpontaneous,
            spontaneousLevel,
            spontaneousSacrificedSpellId);

        if (!consumed)
        {
            Debug.LogError($"[GameManager] {conditionLabel} concentration failure path: could not consume level {slotLevelToConsume} slot for {spell.Name}");
            return false;
        }

        CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} fails the DC {dc} concentration check while {conditionLabel}. {spell.Name} is lost and the spell slot is spent.");
        return false;
    }

    private List<Vector2Int> GetValidFreeAdjacentGrappleMoveSquares(CharacterController actor)
    {
        var validSquares = new List<Vector2Int>();
        if (actor == null || actor.Stats == null || Grid == null)
            return validSquares;

        List<Vector2Int> adjacentSquares = GetAdjacentSquares(actor.GridPosition);
        int actorSize = actor.GetVisualSquaresOccupied();
        for (int i = 0; i < adjacentSquares.Count; i++)
        {
            Vector2Int destination = adjacentSquares[i];
            if (!Grid.CanPlaceCreature(destination, actorSize, actor))
                continue;

            validSquares.Add(destination);
        }

        return validSquares;
    }

    private bool DidActorEndGrappleAndGainFreeAdjacentMove(CharacterController actor, GrappleActionType actionType, SpecialAttackResult result)
    {
        if (actor == null || result == null || !result.Success)
            return false;

        bool wasEscapeAction = actionType == GrappleActionType.EscapeArtist
            || actionType == GrappleActionType.OpposedGrappleEscape;
        if (!wasEscapeAction)
            return false;

        // Escape from pin only removes the pinned state; it does not end the grapple.
        return !actor.IsGrappling();
    }

    private bool TryOfferFreeAdjacentMovementAfterGrappleEnds(CharacterController actor, GrappleActionType actionType, SpecialAttackResult result)
    {
        if (!DidActorEndGrappleAndGainFreeAdjacentMove(actor, actionType, result))
            return false;

        return OfferFreeAdjacentMovement(actor);
    }

    private bool OfferFreeAdjacentMovement(CharacterController actor)
    {
        if (actor == null || actor.Stats == null || actor.Stats.IsDead || Grid == null)
            return false;

        List<Vector2Int> validSquares = GetValidFreeAdjacentGrappleMoveSquares(actor);
        if (validSquares.Count == 0)
        {
            CombatUI?.ShowCombatLog($"{actor.Stats.CharacterName} has no adjacent squares to move to after the grapple ends.");
            return false;
        }

        bool actorCanSelectNow = actor.IsControllable
            && CurrentPhase == TurnPhase.PCTurn
            && ActivePC == actor;

        if (!actorCanSelectNow)
        {
            Vector2Int selectedSquare = validSquares[UnityEngine.Random.Range(0, validSquares.Count)];
            ExecuteFreeAdjacentGrappleMovement(actor, selectedSquare, showActionChoicesAfterMove: false);
            return false;
        }

        BeginFreeAdjacentGrappleMoveSelection(actor, validSquares);
        return true;
    }

    private void ClearFreeAdjacentGrappleMoveSelectionState()
    {
        _isFreeAdjacentGrappleMoveSelection = false;
        _freeAdjacentGrappleMoveActor = null;
        _freeAdjacentGrappleMoveDestinations.Clear();
    }

    private void BeginFreeAdjacentGrappleMoveSelection(CharacterController actor, List<Vector2Int> validSquares)
    {
        if (actor == null || actor.Stats == null || validSquares == null || validSquares.Count == 0)
        {
            ShowActionChoices();
            return;
        }

        ClearOverrunContinuationState();
        ClearGrappleMoveSelectionState();
        ClearFreeAdjacentGrappleMoveSelectionState();

        _isFreeAdjacentGrappleMoveSelection = true;
        _freeAdjacentGrappleMoveActor = actor;
        _freeAdjacentGrappleMoveDestinations.AddRange(validSquares);

        CurrentSubPhase = PlayerSubPhase.Moving;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        for (int i = 0; i < validSquares.Count; i++)
        {
            SquareCell cell = Grid.GetCell(validSquares[i]);
            if (cell == null)
                continue;

            cell.SetHighlight(HighlightType.Move);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(actor, HighlightType.Selected);

        CombatUI?.SetActionButtonsVisible(false);
        CombatUI?.SetTurnIndicator($"{actor.Stats.CharacterName} - Grapple ended: choose a free adjacent square");
        CombatUI?.ShowCombatLog($"{actor.Stats.CharacterName} can move to an adjacent square (free action). Right-click/ESC to remain in place.");
    }

    private bool ExecuteFreeAdjacentGrappleMovement(CharacterController actor, Vector2Int destination, bool showActionChoicesAfterMove)
    {
        if (actor == null || actor.Stats == null || Grid == null)
            return false;

        if (!SquareGridUtils.IsAdjacent(actor.GridPosition, destination))
            return false;

        if (!Grid.CanPlaceCreature(destination, actor.GetVisualSquaresOccupied(), actor))
            return false;

        SquareCell destinationCell = Grid.GetCell(destination);
        if (destinationCell == null)
            return false;

        actor.MoveToCell(destinationCell, markAsMoved: false);

        if (actor.GridPosition != destination)
            return false;

        CombatUI?.ShowCombatLog($"{actor.Stats.CharacterName} moves to ({destination.x},{destination.y}) (free action after ending grapple).");

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();

        if (_isFreeAdjacentGrappleMoveSelection)
        {
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            ClearFreeAdjacentGrappleMoveSelectionState();
        }

        if (showActionChoicesAfterMove)
            ShowActionChoices();

        return true;
    }

    private void HandleFreeAdjacentGrappleMovementClick(CharacterController actor, SquareCell cell)
    {
        if (!_isFreeAdjacentGrappleMoveSelection || actor == null || cell == null)
            return;

        if (_freeAdjacentGrappleMoveActor != actor)
        {
            ClearFreeAdjacentGrappleMoveSelectionState();
            ShowActionChoices();
            return;
        }

        if (cell.Coords == actor.GridPosition)
        {
            CancelFreeAdjacentGrappleMovementSelection(actor);
            return;
        }

        if (!_highlightedCells.Contains(cell) || !_freeAdjacentGrappleMoveDestinations.Contains(cell.Coords))
            return;

        if (!ExecuteFreeAdjacentGrappleMovement(actor, cell.Coords, showActionChoicesAfterMove: true))
            CombatUI?.ShowCombatLog("⚠ Invalid adjacent free-move destination.");
    }

    private void CancelFreeAdjacentGrappleMovementSelection(CharacterController actor)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        ClearFreeAdjacentGrappleMoveSelectionState();

        if (actor != null && actor.Stats != null)
            CombatUI?.ShowCombatLog($"↩ {actor.Stats.CharacterName} remains in place after ending the grapple.");

        ShowActionChoices();
    }

    private void ClearGrappleMoveSelectionState()
    {
        _isGrappleMoveSelection = false;
        _grappleMoveActor = null;
        _grappleMoveMaxRangeSquares = 0;
        _grappleMoveOpponents.Clear();
        _grappleMovePathsByDestination.Clear();
    }

    private void BeginGrappleMoveSelection(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        if (!actor.TryGetActiveGrappleOpponents(out List<CharacterController> opponents)
            || opponents.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} cannot move the grapple: no valid grapple opponents.");
            StartCoroutine(AfterAttackDelay(actor, 0.5f));
            return;
        }

        int halfSpeedRange = Mathf.Max(0, actor.Stats.MoveRange / 2);
        if (halfSpeedRange <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} has no available movement (half speed is 0 squares). Grapple move action is spent.");
            StartCoroutine(AfterAttackDelay(actor, 0.5f));
            return;
        }

        ClearOverrunContinuationState();
        ClearFreeAdjacentGrappleMoveSelectionState();
        ClearGrappleMoveSelectionState();
        _isGrappleMoveSelection = true;
        _grappleMoveActor = actor;
        _grappleMoveMaxRangeSquares = halfSpeedRange;
        _grappleMoveOpponents.AddRange(opponents);

        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowGrappleMoveRange(actor);
        CombatUI?.SetActionButtonsVisible(false);

        if (_highlightedCells.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} beats the grapple check but has no valid destination within half speed ({halfSpeedRange} squares) while dragging grappled opponents.");
            ClearGrappleMoveSelectionState();
            StartCoroutine(AfterAttackDelay(actor, 0.5f));
            return;
        }

        CombatUI?.SetTurnIndicator($"{actor.Stats.CharacterName} - Move while grappling: choose destination within half speed ({halfSpeedRange} sq)");
        CombatUI?.ShowCombatLog($"↔ Move while grappling: select any highlighted square up to half speed ({halfSpeedRange} squares). Grappled opponents will be dragged with you.");
    }

    private void ShowGrappleMoveRange(CharacterController actor)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _grappleMovePathsByDestination.Clear();

        if (actor == null || actor.Stats == null)
            return;

        List<CharacterController> temporarilyClearedOpponents = TemporarilyClearOccupancy(_grappleMoveOpponents);
        try
        {
            List<SquareCell> candidateCells = Grid.GetCellsInRange(actor.GridPosition, _grappleMoveMaxRangeSquares);
            foreach (SquareCell candidate in candidateCells)
            {
                if (candidate == null || candidate.Coords == actor.GridPosition)
                    continue;

                if (!TryBuildGrappleMovePath(actor, candidate.Coords, out List<Vector2Int> path))
                    continue;

                candidate.SetHighlight(HighlightType.Move);
                _highlightedCells.Add(candidate);
                _grappleMovePathsByDestination[candidate.Coords] = path;
            }
        }
        finally
        {
            RestoreOccupancy(temporarilyClearedOpponents);
        }

        HighlightCharacterFootprint(actor, HighlightType.Selected);
    }

    private List<CharacterController> TemporarilyClearOccupancy(List<CharacterController> characters)
    {
        var cleared = new List<CharacterController>();
        if (Grid == null || characters == null)
            return cleared;

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterController character = characters[i];
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            Grid.ClearCreatureOccupancy(character);
            cleared.Add(character);
        }

        return cleared;
    }

    private void RestoreOccupancy(List<CharacterController> characters)
    {
        if (Grid == null || characters == null)
            return;

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterController character = characters[i];
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            Grid.SetCreatureOccupancy(character, character.GridPosition, character.GetVisualSquaresOccupied());
        }
    }

    private bool TryBuildGrappleMovePath(CharacterController actor, Vector2Int destination, out List<Vector2Int> path)
    {
        path = null;
        if (actor == null || actor.Stats == null)
            return false;

        HashSet<Vector2Int> threatenedSquares = GetPreviewThreatenedSquares(actor);
        AoOPathResult pathResult = FindPath(
            actor,
            destination,
            threatenedSquares,
            _grappleMoveMaxRangeSquares,
            allowThroughAllies: true,
            allowThroughEnemies: false);

        if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
            return false;

        if (!IsValidGrappleGroupTranslation(actor, destination))
            return false;

        path = new List<Vector2Int>(pathResult.Path);
        return true;
    }

    private bool IsValidGrappleGroupTranslation(CharacterController actor, Vector2Int actorDestination)
    {
        if (actor == null || Grid == null)
            return false;

        Vector2Int delta = actorDestination - actor.GridPosition;
        var preferredOpponentDestinations = new Dictionary<CharacterController, Vector2Int>();

        for (int i = 0; i < _grappleMoveOpponents.Count; i++)
        {
            CharacterController opponent = _grappleMoveOpponents[i];
            if (opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
                continue;

            preferredOpponentDestinations[opponent] = opponent.GridPosition + delta;
        }

        return TryResolveGrappleGroupDestinations(actor, actorDestination, preferredOpponentDestinations, out _);
    }

    private bool TryResolveGrappleGroupDestinations(
        CharacterController actor,
        Vector2Int actorDestination,
        Dictionary<CharacterController, Vector2Int> preferredOpponentDestinations,
        out Dictionary<CharacterController, Vector2Int> resolvedOpponentDestinations)
    {
        resolvedOpponentDestinations = new Dictionary<CharacterController, Vector2Int>();
        if (actor == null || actor.Stats == null || Grid == null)
            return false;

        var movingParticipants = new HashSet<CharacterController> { actor };
        if (preferredOpponentDestinations != null)
        {
            foreach (CharacterController opponent in preferredOpponentDestinations.Keys)
            {
                if (opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
                    continue;
                movingParticipants.Add(opponent);
            }
        }

        var claimedSquares = new Dictionary<Vector2Int, CharacterController>();
        if (!TryClaimDestinationSquares(actor, actorDestination, movingParticipants, claimedSquares))
            return false;

        if (preferredOpponentDestinations == null)
            return true;

        var neighborCandidates = new List<Vector2Int>(SquareGridUtils.GetNeighbors(actorDestination));

        foreach (KeyValuePair<CharacterController, Vector2Int> kvp in preferredOpponentDestinations)
        {
            CharacterController opponent = kvp.Key;
            if (opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
                continue;

            Vector2Int preferredDestination = kvp.Value;
            bool foundPlacement = false;

            if (TryClaimDestinationSquares(opponent, preferredDestination, movingParticipants, claimedSquares))
            {
                resolvedOpponentDestinations[opponent] = preferredDestination;
                continue;
            }

            int bestIndex = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < neighborCandidates.Count; i++)
            {
                Vector2Int candidate = neighborCandidates[i];
                int distance = SquareGridUtils.GetDistance(candidate, preferredDestination);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0 && bestIndex < neighborCandidates.Count)
            {
                Vector2Int prioritized = neighborCandidates[bestIndex];
                neighborCandidates.RemoveAt(bestIndex);
                neighborCandidates.Insert(0, prioritized);
            }

            for (int i = 0; i < neighborCandidates.Count; i++)
            {
                Vector2Int fallbackDestination = neighborCandidates[i];
                if (!TryClaimDestinationSquares(opponent, fallbackDestination, movingParticipants, claimedSquares))
                    continue;

                resolvedOpponentDestinations[opponent] = fallbackDestination;
                foundPlacement = true;
                break;
            }

            if (!foundPlacement)
                return false;
        }

        return true;
    }

    private bool TryClaimDestinationSquares(
        CharacterController participant,
        Vector2Int destination,
        HashSet<CharacterController> movingParticipants,
        Dictionary<Vector2Int, CharacterController> claimedSquares)
    {
        if (participant == null || participant.Stats == null || Grid == null)
            return false;

        int sizeSquares = participant.GetVisualSquaresOccupied();
        if (!Grid.CanPlaceCreature(destination, sizeSquares, participant, ignoreOtherOccupants: true))
            return false;

        List<Vector2Int> occupied = Grid.GetOccupiedSquares(destination, sizeSquares);
        for (int squareIndex = 0; squareIndex < occupied.Count; squareIndex++)
        {
            Vector2Int square = occupied[squareIndex];
            if (!Grid.IsValidPosition(square))
                return false;

            if (claimedSquares.TryGetValue(square, out CharacterController existingOwner) && existingOwner != participant)
                return false;

            SquareCell cell = Grid.GetCell(square);
            if (cell == null)
                return false;

            CharacterController occupant = cell.Occupant;
            if (cell.IsOccupied && occupant != null && !movingParticipants.Contains(occupant))
                return false;
        }

        for (int squareIndex = 0; squareIndex < occupied.Count; squareIndex++)
            claimedSquares[occupied[squareIndex]] = participant;

        return true;
    }

    private void HandleGrappleMovementClick(CharacterController actor, SquareCell cell)
    {
        if (!_isGrappleMoveSelection || actor == null || cell == null)
            return;

        if (_grappleMoveActor != actor)
        {
            ClearGrappleMoveSelectionState();
            ShowActionChoices();
            return;
        }

        if (cell.Coords == actor.GridPosition)
        {
            CancelGrappleMoveSelection(actor);
            return;
        }

        if (!_highlightedCells.Contains(cell))
            return;

        if (!_grappleMovePathsByDestination.TryGetValue(cell.Coords, out List<Vector2Int> selectedPath)
            || selectedPath == null
            || selectedPath.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ Invalid grapple move destination.");
            return;
        }

        Vector2Int destination = selectedPath[selectedPath.Count - 1];
        CombatUI?.ShowCombatLog($"↔ {actor.Stats.CharacterName} selects grapple move destination ({destination.x},{destination.y}).");
        StartCoroutine(ExecuteGrappleMovement(actor, selectedPath));
    }

    private IEnumerator ExecuteGrappleMovement(CharacterController actor, List<Vector2Int> actorPath)
    {
        if (actor == null || actor.Stats == null || actorPath == null || actorPath.Count == 0)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;

        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        Vector2Int actorStart = actor.GridPosition;
        var opponentStartPositions = new Dictionary<CharacterController, Vector2Int>();
        var movedOpponents = new List<CharacterController>();

        for (int i = 0; i < _grappleMoveOpponents.Count; i++)
        {
            CharacterController opponent = _grappleMoveOpponents[i];
            if (opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
                continue;

            opponentStartPositions[opponent] = opponent.GridPosition;
            movedOpponents.Add(opponent);
        }

        List<CharacterController> temporarilyClearedOpponents = TemporarilyClearOccupancy(movedOpponents);

        yield return StartCoroutine(actor.MoveAlongPath(actorPath, PlayerMoveSecondsPerStep, markAsMoved: true));

        Vector2Int movementDelta = actor.GridPosition - actorStart;
        var draggedMessages = new List<string>();

        if (movementDelta != Vector2Int.zero && movedOpponents.Count > 0)
        {
            var preferredOpponentDestinations = new Dictionary<CharacterController, Vector2Int>();
            for (int i = 0; i < movedOpponents.Count; i++)
            {
                CharacterController opponent = movedOpponents[i];
                if (!opponentStartPositions.TryGetValue(opponent, out Vector2Int opponentStart))
                    continue;

                preferredOpponentDestinations[opponent] = opponentStart + movementDelta;
            }

            if (TryResolveGrappleGroupDestinations(actor, actor.GridPosition, preferredOpponentDestinations, out Dictionary<CharacterController, Vector2Int> resolvedOpponentDestinations))
            {
                var movedSet = new HashSet<CharacterController>();
                for (int i = 0; i < movedOpponents.Count; i++)
                {
                    CharacterController opponent = movedOpponents[i];
                    if (!resolvedOpponentDestinations.TryGetValue(opponent, out Vector2Int opponentDestination))
                        continue;

                    SquareCell destinationCell = Grid.GetCell(opponentDestination);
                    if (destinationCell == null)
                    {
                        CombatUI?.ShowCombatLog($"⚠ Grapple drag failed for {opponent.Stats.CharacterName}: invalid destination square.");
                        continue;
                    }

                    opponent.MoveToCell(destinationCell, markAsMoved: false);
                    if (opponent.GridPosition == opponentDestination)
                    {
                        movedSet.Add(opponent);
                        draggedMessages.Add($"{opponent.Stats.CharacterName} dragged to ({opponentDestination.x},{opponentDestination.y})");
                    }
                    else if (opponentStartPositions.TryGetValue(opponent, out Vector2Int failedStart))
                    {
                        Grid.SetCreatureOccupancy(opponent, failedStart, opponent.GetVisualSquaresOccupied());
                        CombatUI?.ShowCombatLog($"⚠ Grapple drag failed for {opponent.Stats.CharacterName}: destination blocked.");
                    }
                }

                for (int i = 0; i < movedOpponents.Count; i++)
                {
                    CharacterController opponent = movedOpponents[i];
                    if (movedSet.Contains(opponent))
                        continue;

                    if (opponentStartPositions.TryGetValue(opponent, out Vector2Int startPosition))
                        Grid.SetCreatureOccupancy(opponent, startPosition, opponent.GetVisualSquaresOccupied());
                }
            }
            else
            {
                for (int i = 0; i < movedOpponents.Count; i++)
                {
                    CharacterController opponent = movedOpponents[i];
                    if (opponentStartPositions.TryGetValue(opponent, out Vector2Int startPosition))
                        Grid.SetCreatureOccupancy(opponent, startPosition, opponent.GetVisualSquaresOccupied());
                }

                CombatUI?.ShowCombatLog("⚠ Grapple drag destination became invalid; opponents remain in place.");
            }
        }
        else
        {
            RestoreOccupancy(temporarilyClearedOpponents);
        }

        CombatUI?.ShowCombatLog($"↔ {actor.Stats.CharacterName} moves while grappling from ({actorStart.x},{actorStart.y}) to ({actor.GridPosition.x},{actor.GridPosition.y}).");
        for (int i = 0; i < draggedMessages.Count; i++)
            CombatUI?.ShowCombatLog($"   • {draggedMessages[i]}");

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();

        ClearGrappleMoveSelectionState();
        ShowActionChoices();
    }

    private void CancelGrappleMoveSelection(CharacterController actor)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        ClearGrappleMoveSelectionState();

        if (actor != null && actor.Stats != null)
            CombatUI?.ShowCombatLog($"↩ {actor.Stats.CharacterName} chooses not to move after winning grapple control.");

        ShowActionChoices();
    }

    private void BeginGrappleContextMenuDisplayLock(CharacterController actor)
    {
        if (_grappleContextMenuLockOwner == actor)
            return;

        EndGrappleContextMenuDisplayLock();

        if (actor == null)
            return;

        actor.SetGrappleContextMenuDisplayLocked(true);
        _grappleContextMenuLockOwner = actor;
    }

    private void EndGrappleContextMenuDisplayLock()
    {
        if (_grappleContextMenuLockOwner == null)
            return;

        _grappleContextMenuLockOwner.SetGrappleContextMenuDisplayLocked(false);
        _grappleContextMenuLockOwner = null;
    }

    private void ShowGrappleActionMenu(CharacterController actor, CharacterController opponent)
    {
        LogMenuFlow("ShowGrappleActionMenu:ENTER", actor, $"opponent={(opponent != null && opponent.Stats != null ? opponent.Stats.CharacterName : "<null>")}");

        if (actor == null || opponent == null)
        {
            LogMenuFlow("ShowGrappleActionMenu:NULL_ACTOR_OR_OPPONENT", actor);
            ShowActionChoices();
            return;
        }

        if (!actor.TryGetGrappleState(out CharacterController liveOpponent, out _, out bool isPinned, out bool opponentPinned))
        {
            LogMenuFlow("ShowGrappleActionMenu:NO_LONGER_GRAPPLING", actor);
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} is no longer in a grapple.");
            ShowActionChoices();
            return;
        }

        opponent = liveOpponent;
        BeginGrappleContextMenuDisplayLock(actor);
        bool isPinning = actor.IsPinningOpponent();

        var options = new List<(GrappleActionType Action, string Label, bool Enabled, string DisabledMessage)>();

        if (isPinned)
        {
            bool hasStandardAction = actor.Actions != null && actor.Actions.HasStandardAction;
            bool hasIterativeAttack = CanUseGrappleAttackOption(actor);
            options.Add((
                GrappleActionType.EscapeArtist,
                $"Escape Artist check (Std): d20 + Escape Artist vs DC 20 + {opponent.Stats.CharacterName}'s grapple mod ({opponent.GetGrappleModifier():+#;-#;0})",
                hasStandardAction,
                hasStandardAction ? string.Empty : "Standard action already spent."));
            options.Add((
                GrappleActionType.OpposedGrappleEscape,
                $"Break grapple (opposed grapple check, {CharacterStats.FormatMod(GetCurrentGrappleAttackBonus(actor))} BAB)",
                hasIterativeAttack,
                hasIterativeAttack ? string.Empty : "No shared grapple attacks remaining."));
        }
        else
        {
            List<DisarmableHeldItemOption> opponentLightWeaponOptions = opponent.GetEquippedLightHandWeaponOptions();
            bool hasOpponentLightWeapon = opponentLightWeaponOptions != null && opponentLightWeaponOptions.Count > 0;

            void AddOption(GrappleActionType actionType, string label, bool requiresStandardAction = true)
            {
                bool enabled = CanUseGrappleAction(actor, actionType);
                string disabledReason = enabled ? string.Empty : "Action is not allowed in the current pin state.";

                if (enabled)
                {
                    if (CharacterController.IsIterativeGrappleAttackAction(actionType))
                    {
                        enabled = CanUseGrappleAttackOption(actor);
                        if (!enabled)
                            disabledReason = "No shared grapple attacks remaining.";
                    }
                    else if (requiresStandardAction)
                    {
                        enabled = actor.Actions != null && actor.Actions.HasStandardAction;
                        if (!enabled)
                            disabledReason = "Standard action already spent.";
                    }
                }

                string displayLabel = enabled ? label : $"{label} (Unavailable)";
                options.Add((actionType, displayLabel, enabled, disabledReason));
            }

            if (isPinning)
            {
                AddOption(GrappleActionType.DamageOpponent, $"Damage {opponent.Stats.CharacterName} (opposed grapple check)");

                string useWeaponLabel = hasOpponentLightWeapon
                    ? $"Use {opponent.Stats.CharacterName}'s Weapon"
                    : $"Use Opponent Weapon (Unavailable: {opponent.Stats.CharacterName} has no equipped light weapon)";
                bool canUseOpponentWeapon = hasOpponentLightWeapon;
                if (!canUseOpponentWeapon)
                {
                    options.Add((
                        GrappleActionType.UseOpponentWeapon,
                        useWeaponLabel,
                        false,
                        $"{opponent.Stats.CharacterName} has no equipped light weapon."));
                }
                else
                {
                    AddOption(GrappleActionType.UseOpponentWeapon, useWeaponLabel);
                }

                AddOption(GrappleActionType.MoveHalfSpeed, $"Move (dragging {opponent.Stats.CharacterName})");
                AddOption(GrappleActionType.DisarmSmallObject, $"Disarm Small Object from {opponent.Stats.CharacterName} (stub)");

                bool canReleasePinnedOpponent = opponentPinned;
                options.Add((
                    GrappleActionType.ReleasePinnedOpponent,
                    canReleasePinnedOpponent
                        ? $"Release {opponent.Stats.CharacterName} from pin (remain grappling)"
                        : "Release Pinned Opponent (No pinned opponent)",
                    canReleasePinnedOpponent,
                    canReleasePinnedOpponent ? string.Empty : "No pinned opponent to release."));
            }
            else
            {
                AddOption(GrappleActionType.DamageOpponent, "Deal grapple damage (opposed check + unarmed strike damage; choose lethal/nonlethal)");

                bool actorHasMainHandLightWeapon = actor.CanAttackWithLightWeaponWhileGrappling(out ItemData actorMainHandLightWeapon, out string actorLightWeaponReason);
                options.Add((
                    GrappleActionType.AttackWithLightWeapon,
                    actorHasMainHandLightWeapon
                        ? $"Attack with Light Weapon: {actorMainHandLightWeapon.Name} (-4 attack roll, no grapple check)"
                        : $"Attack with Light Weapon (Unavailable: {actorLightWeaponReason})",
                    actorHasMainHandLightWeapon && CanUseGrappleAttackOption(actor),
                    actorHasMainHandLightWeapon ? string.Empty : actorLightWeaponReason));

                bool actorCanAttackUnarmed = actor.CanAttackUnarmedWhileGrappling(out string actorUnarmedReason);
                options.Add((
                    GrappleActionType.AttackUnarmed,
                    actorCanAttackUnarmed
                        ? "Attack Unarmed (-4 attack roll, no grapple check)"
                        : $"Attack Unarmed (Unavailable: {actorUnarmedReason})",
                    actorCanAttackUnarmed && CanUseGrappleAttackOption(actor),
                    actorCanAttackUnarmed ? string.Empty : actorUnarmedReason));

                string useWeaponLabel = hasOpponentLightWeapon
                    ? "Use Opponent's Weapon (opposed grapple check, then immediate attack at -4; choose weapon hand)"
                    : $"Use Opponent's Weapon (Unavailable: {opponent.Stats.CharacterName} has no equipped light weapon)";
                options.Add((
                    GrappleActionType.UseOpponentWeapon,
                    useWeaponLabel,
                    hasOpponentLightWeapon && CanUseGrappleAttackOption(actor),
                    hasOpponentLightWeapon ? string.Empty : $"{opponent.Stats.CharacterName} has no equipped light weapon."));

                AddOption(GrappleActionType.PinOpponent, $"Pin {opponent.Stats.CharacterName} (opposed grapple check)");
                AddOption(GrappleActionType.BreakPin, $"Break pin (opposed grapple check vs {opponent.Stats.CharacterName})");
                AddOption(GrappleActionType.DrawLightWeapon, "Draw a Light Weapon (Not yet implemented)");
                AddOption(GrappleActionType.RetrieveSpellComponent, "Retrieve a Spell Component (Not yet implemented)");
                AddOption(GrappleActionType.MoveHalfSpeed, "Move while grappling (standard action, beat opposed grapple check(s), then move at half speed)");
                AddOption(GrappleActionType.OpposedGrappleEscape, $"Break grapple (opposed grapple check vs {opponent.Stats.CharacterName})");
                AddOption(GrappleActionType.EscapeArtist, $"Escape Artist check (d20 + Escape Artist) vs DC 20 + {opponent.Stats.CharacterName}'s grapple mod ({opponent.GetGrappleModifier():+#;-#;0})");
            }
        }

        List<string> optionLabels = new List<string>(options.Count);
        List<bool> optionEnabledStates = new List<bool>(options.Count);
        foreach (var option in options)
        {
            optionLabels.Add(option.Enabled ? option.Label : $"{option.Label} (Unavailable)");
            optionEnabledStates.Add(option.Enabled);
        }

        LogMenuFlow("ShowGrappleActionMenu:SHOW_MENU", actor, $"optionCount={options.Count}, isPinned={isPinned}, opponentPinned={opponentPinned}, isPinning={isPinning}");

        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.ShowSpecialStyleSelectionMenu(
            menuName: "GrappleActionMenu",
            optionLabels: optionLabels,
            optionEnabledStates: optionEnabledStates,
            onSelect: selectedIndex =>
            {
                LogMenuFlow("ShowGrappleActionMenu:OPTION_SELECTED", actor, $"selectedIndex={selectedIndex}");

                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    LogMenuFlow("ShowGrappleActionMenu:OPTION_INVALID", actor, $"selectedIndex={selectedIndex}");
                    ShowActionChoices();
                    return;
                }

                if (!options[selectedIndex].Enabled)
                {
                    string blockedMessage = string.IsNullOrEmpty(options[selectedIndex].DisabledMessage)
                        ? options[selectedIndex].Label
                        : options[selectedIndex].DisabledMessage;
                    LogMenuFlow("ShowGrappleActionMenu:OPTION_BLOCKED", actor, $"selectedIndex={selectedIndex}, reason={blockedMessage}");
                    CombatUI?.ShowCombatLog($"⚠ {blockedMessage}");
                    ShowGrappleActionMenu(actor, opponent);
                    return;
                }

                ResolveGrappleActionSelection(actor, options[selectedIndex].Action);
            },
            onCancel: () =>
            {
                LogMenuFlow("ShowGrappleActionMenu:CANCEL", actor);
                ShowActionChoices();
            });
    }

    private void ResolveGrappleActionSelection(CharacterController actor, GrappleActionType actionType)
    {
        LogMenuFlow("ResolveGrappleActionSelection:ENTER", actor, $"actionType={actionType}");

        if (actor == null || actor.Stats == null)
        {
            LogMenuFlow("ResolveGrappleActionSelection:NULL_ACTOR", actor, $"actionType={actionType}");
            ShowActionChoices();
            return;
        }

        if (actionType == GrappleActionType.DamageOpponent)
        {
            ShowGrappleDamageModeMenu(actor);
            return;
        }

        if (actionType == GrappleActionType.UseOpponentWeapon)
        {
            ShowUseOpponentWeaponHandSelectionMenu(actor);
            return;
        }

        ExecuteGrappleAction(actor, actionType);
    }

    private void ShowUseOpponentWeaponHandSelectionMenu(CharacterController actor)
    {
        LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:ENTER", actor);

        if (actor == null || actor.Stats == null)
        {
            LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:NULL_ACTOR", actor);
            ShowActionChoices();
            return;
        }

        if (!actor.TryGetGrappleState(out CharacterController opponent, out _, out bool isPinned, out _))
        {
            LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:NO_LONGER_GRAPPLING", actor);
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} is no longer in a grapple.");
            ShowActionChoices();
            return;
        }

        if (isPinned)
        {
            LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:ACTOR_PINNED", actor);
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} is pinned and cannot use opponent's weapon.");
            ShowActionChoices();
            return;
        }

        BeginGrappleContextMenuDisplayLock(actor);

        List<DisarmableHeldItemOption> options = opponent.GetEquippedLightHandWeaponOptions();
        if (options == null || options.Count == 0)
        {
            LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:NO_OPTIONS", actor, $"opponent={opponent.Stats.CharacterName}");
            CombatUI?.ShowCombatLog($"⚠ {opponent.Stats.CharacterName} has no equipped light weapon to use.");
            ShowActionChoices();
            return;
        }

        var labels = new List<string>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            string handLabel = options[i].HandSlot == EquipSlot.RightHand ? "Right Hand" : "Left Hand";
            labels.Add($"{options[i].HeldItem.Name} ({handLabel}, light weapon)");
        }

        var enabledStates = new List<bool>(labels.Count);
        for (int i = 0; i < labels.Count; i++)
            enabledStates.Add(true);

        LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:SHOW_MENU", actor, $"optionCount={options.Count}");

        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.ShowSpecialStyleSelectionMenu(
            menuName: "GrappleUseWeaponMenu",
            optionLabels: labels,
            optionEnabledStates: enabledStates,
            onSelect: selectedIndex =>
            {
                LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:OPTION_SELECTED", actor, $"selectedIndex={selectedIndex}");

                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:OPTION_INVALID", actor, $"selectedIndex={selectedIndex}");
                    ShowActionChoices();
                    return;
                }

                ExecuteGrappleAction(actor, GrappleActionType.UseOpponentWeapon, null, options[selectedIndex].HandSlot);
            },
            onCancel: () =>
            {
                LogMenuFlow("ShowUseOpponentWeaponHandSelectionMenu:CANCEL", actor);
                ShowActionChoices();
            });
    }
    private void ShowGrappleDamageModeMenu(CharacterController actor)
    {
        LogMenuFlow("ShowGrappleDamageModeMenu:ENTER", actor);

        if (actor == null || actor.Stats == null)
        {
            LogMenuFlow("ShowGrappleDamageModeMenu:NULL_ACTOR", actor);
            ShowActionChoices();
            return;
        }

        if (!actor.TryGetGrappleState(out CharacterController opponent, out _, out _, out _))
        {
            LogMenuFlow("ShowGrappleDamageModeMenu:NO_LONGER_GRAPPLING", actor);
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} is no longer in a grapple.");
            ShowActionChoices();
            return;
        }

        BeginGrappleContextMenuDisplayLock(actor);

        bool isMonk = actor.Stats.IsMonk;
        var options = new List<(AttackDamageMode mode, string label)>
        {
            isMonk
                ? (AttackDamageMode.Lethal, "Lethal damage (Monk default, no grapple penalty)")
                : (AttackDamageMode.Nonlethal, "Nonlethal damage (default, no grapple penalty)"),
            isMonk
                ? (AttackDamageMode.Nonlethal, "Nonlethal damage (Monk can choose this with no penalty)")
                : (AttackDamageMode.Lethal, "Lethal damage (-4 penalty on your grapple check)")
        };

        var labels = new List<string>(options.Count);
        foreach (var option in options)
            labels.Add(option.label);

        var enabledStates = new List<bool>(labels.Count);
        for (int i = 0; i < labels.Count; i++)
            enabledStates.Add(true);

        LogMenuFlow("ShowGrappleDamageModeMenu:SHOW_MENU", actor, $"optionCount={options.Count}");

        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.ShowSpecialStyleSelectionMenu(
            menuName: "GrappleDamageModeMenu",
            optionLabels: labels,
            optionEnabledStates: enabledStates,
            onSelect: selectedIndex =>
            {
                LogMenuFlow("ShowGrappleDamageModeMenu:OPTION_SELECTED", actor, $"selectedIndex={selectedIndex}");

                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    LogMenuFlow("ShowGrappleDamageModeMenu:OPTION_INVALID", actor, $"selectedIndex={selectedIndex}");
                    ShowActionChoices();
                    return;
                }

                ExecuteGrappleAction(actor, GrappleActionType.DamageOpponent, options[selectedIndex].mode);
            },
            onCancel: () =>
            {
                LogMenuFlow("ShowGrappleDamageModeMenu:CANCEL", actor);
                ShowActionChoices();
            });
    }

    [ContextMenu("Debug/Test Menu Stability")]
    private void TestMenuStability()
    {
        LogMenuFlow("TestMenuStability:START", ActivePC);

        CombatUI?.ShowSpecialStyleSelectionMenu(
            menuName: "DebugTestMenu",
            optionLabels: new List<string> { "Option 1", "Option 2" },
            optionEnabledStates: new List<bool> { true, true },
            onSelect: index => Debug.Log($"[GameManager][MenuFlow] TestMenuStability:SELECTED index={index}"),
            onCancel: () => Debug.Log("[GameManager][MenuFlow] TestMenuStability:CANCELLED"));

        StartCoroutine(CheckMenuAfterDelay());
    }

    private IEnumerator CheckMenuAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        LogMenuFlow("TestMenuStability:CHECK", ActivePC, $"t=0.1s, visible={CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen()}");

        yield return new WaitForSeconds(0.5f);
        LogMenuFlow("TestMenuStability:CHECK", ActivePC, $"t=0.6s, visible={CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen()}");

        yield return new WaitForSeconds(1.0f);
        LogMenuFlow("TestMenuStability:CHECK", ActivePC, $"t=1.6s, visible={CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen()}");
    }

    private bool TryConsumeIterativeGrappleAttack(CharacterController actor, GrappleActionType actionType, out int attackBonusUsed, out int attacksRemaining)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;

        if (actor == null || actor.Stats == null)
            return false;

        if (!TryConsumeGrappleAttackAction(actor, out attackBonusUsed, out attacksRemaining, out string reason))
        {
            string fallback = string.IsNullOrWhiteSpace(reason)
                ? "no grapple attacks remain in the shared pool"
                : reason;
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} cannot use {actionType}: {fallback}");
            Debug.LogWarning($"[GameManager][Grapple] Shared-pool consume failed for {actor.Stats.CharacterName} action={actionType} reason={fallback}");
            return false;
        }

        Debug.Log($"[GameManager][Grapple] Shared-pool consume success actor={actor.Stats.CharacterName} action={actionType} usedBAB={CharacterStats.FormatMod(attackBonusUsed)} remaining={attacksRemaining}");
        return true;
    }

    private bool ShouldPromptPlayerForGrappleEscapeOpposition(CharacterController escaper, out CharacterController grappler)
    {
        grappler = null;
        if (escaper == null || escaper.Stats == null)
            return false;

        if (!escaper.TryGetGrappleState(out CharacterController opponent, out _, out _, out _))
            return false;

        if (opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
            return false;

        if (!opponent.IsControllable)
            return false;

        grappler = opponent;
        return true;
    }

    private string BuildGrappleEscapeOppositionPromptMessage(CharacterController escaper, CharacterController grappler)
    {
        string escaperName = escaper != null && escaper.Stats != null ? escaper.Stats.CharacterName : "Escaper";
        string grapplerName = grappler != null && grappler.Stats != null ? grappler.Stats.CharacterName : "Grappler";
        int escaperGrappleMod = escaper != null ? escaper.GetGrappleModifier() : 0;
        int escaperEscapeArtistMod = escaper != null && escaper.Stats != null ? escaper.Stats.GetSkillBonus("Escape Artist") : 0;
        int grapplerGrappleMod = grappler != null ? grappler.GetGrappleModifier() : 0;

        return $"{escaperName} is attempting to escape your grapple!\n\n"
            + "Choose whether to oppose the escape or let them go.\n\n"
            + $"{escaperName} grapple mod: {CharacterStats.FormatMod(escaperGrappleMod)}\n"
            + $"{escaperName} Escape Artist: {CharacterStats.FormatMod(escaperEscapeArtistMod)}\n"
            + $"{grapplerName} grapple mod: {CharacterStats.FormatMod(grapplerGrappleMod)}";
    }

    private SpecialAttackResult BuildAllowedGrappleEscapeResult(CharacterController escaper, CharacterController grappler)
    {
        string escaperName = escaper != null && escaper.Stats != null ? escaper.Stats.CharacterName : "Escaper";
        string grapplerName = grappler != null && grappler.Stats != null ? grappler.Stats.CharacterName : "Grappler";

        return new SpecialAttackResult
        {
            ManeuverName = "Grapple Escape (Allowed)",
            Success = true,
            Log = string.Join("\n\n", new[]
            {
                $"{escaperName} attempts to escape from grapple",
                $"{grapplerName} chooses not to oppose the escape.",
                $"{escaperName} escapes from grapple without an opposed check."
            })
        };
    }

    private bool TryResolveOpposedGrappleEscapeWithPrompt(
        CharacterController escaper,
        int? iterativeAttackBonusOverride,
        Action<SpecialAttackResult> onResolved)
    {
        if (!ShouldPromptPlayerForGrappleEscapeOpposition(escaper, out CharacterController grappler))
            return false;

        if (escaper == null || escaper.Stats == null || grappler == null || grappler.Stats == null)
            return false;

        if (CombatUI == null)
            return false;

        string escaperName = escaper.Stats.CharacterName;
        string grapplerName = grappler.Stats.CharacterName;

        CombatUI?.ShowCombatLog($"{escaperName} attempts to escape {grapplerName}'s grapple!");
        CombatUI?.ShowCombatLog($"Waiting for {grapplerName}'s decision...");

        CombatUI?.ShowConfirmationDialog(
            title: "Escape Attempt",
            message: BuildGrappleEscapeOppositionPromptMessage(escaper, grappler),
            confirmLabel: "Oppose Escape",
            cancelLabel: "Allow Escape",
            onConfirm: () =>
            {
                CombatUI?.ShowCombatLog($"{grapplerName} opposes the escape attempt!");
                SpecialAttackResult opposedResult = escaper.ResolveGrappleAction(
                    GrappleActionType.OpposedGrappleEscape,
                    null,
                    null,
                    iterativeAttackBonusOverride);
                onResolved?.Invoke(opposedResult);
            },
            onCancel: () =>
            {
                CombatUI?.ShowCombatLog($"{grapplerName} allows {escaperName} to escape.");
                escaper.ReleaseGrappleState("escape allowed by grappler");
                onResolved?.Invoke(BuildAllowedGrappleEscapeResult(escaper, grappler));
            });

        return true;
    }

    private void FinalizeGrappleActionResolution(
        CharacterController actor,
        GrappleActionType actionType,
        SpecialAttackResult result,
        bool isFreeAction,
        bool usesIterativeAttack,
        int attackBonusUsed,
        int attacksRemaining)
    {
        if (actor == null || actor.Stats == null || result == null)
        {
            ShowActionChoices();
            return;
        }

        string actionCostLabel = isFreeAction
            ? " [Free Action]"
            : (usesIterativeAttack
                ? $" [Attack BAB {CharacterStats.FormatMod(attackBonusUsed)} | {attacksRemaining} left]"
                : " [Standard Action]");
        string grappleHeader = $"⚔ GRAPPLE [{actionType}]{actionCostLabel}";
        if (!string.IsNullOrEmpty(result.Log) && result.Log.Contains("\n"))
        {
            CombatUI?.ShowCombatLog(grappleHeader);
            CombatUI?.ShowCombatLog(result.Log);
        }
        else
        {
            CombatUI?.ShowCombatLog($"{grappleHeader}: {result.Log}");
        }

        UpdateAllStatsUI();

        if (actionType == GrappleActionType.MoveHalfSpeed && result.Success)
        {
            BeginGrappleMoveSelection(actor);
            return;
        }

        if (TryOfferFreeAdjacentMovementAfterGrappleEnds(actor, actionType, result))
            return;

        float delay = isFreeAction ? 0.35f : (usesIterativeAttack ? 0.45f : 0.8f);
        LogMenuFlow("ExecuteGrappleAction:SCHEDULE_AFTER_ATTACK_DELAY", actor, $"actionType={actionType}, delay={delay:0.00}");
        StartCoroutine(AfterAttackDelay(actor, delay));
    }

    private void ExecuteGrappleAction(
        CharacterController actor,
        GrappleActionType actionType,
        AttackDamageMode? grappleDamageModeOverride = null,
        EquipSlot? opponentWeaponHandSlotOverride = null)
    {
        LogMenuFlow("ExecuteGrappleAction:ENTER", actor, $"actionType={actionType}, damageMode={grappleDamageModeOverride}, handOverride={opponentWeaponHandSlotOverride}");

        if (actor == null || actor.Stats == null)
        {
            LogMenuFlow("ExecuteGrappleAction:NULL_ACTOR", actor, $"actionType={actionType}");
            ShowActionChoices();
            return;
        }

        if (!CanUseGrappleAction(actor, actionType))
        {
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} cannot use {actionType} in the current pin state.");
            ShowActionChoices();
            return;
        }

        if (actor.IsGrappleActionBlockedWhilePinning(actionType, out string blockedReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {blockedReason}");
            ShowActionChoices();
            return;
        }

        EndGrappleContextMenuDisplayLock();

        bool isFreeAction = actionType == GrappleActionType.ReleasePinnedOpponent;
        bool usesIterativeAttack = CharacterController.IsIterativeGrappleAttackAction(actionType);
        int attackBonusUsed = 0;
        int attacksRemaining = 0;

        if (usesIterativeAttack)
        {
            if (!TryConsumeIterativeGrappleAttack(actor, actionType, out attackBonusUsed, out attacksRemaining))
            {
                ShowActionChoices();
                return;
            }
        }
        else if (!isFreeAction && !actor.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} cannot use grapple action: standard action already spent.");
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        if (actionType == GrappleActionType.OpposedGrappleEscape
            && TryResolveOpposedGrappleEscapeWithPrompt(
                actor,
                usesIterativeAttack ? attackBonusUsed : (int?)null,
                onResolved: result => FinalizeGrappleActionResolution(actor, actionType, result, isFreeAction, usesIterativeAttack, attackBonusUsed, attacksRemaining)))
        {
            return;
        }

        SpecialAttackResult directResult = actor.ResolveGrappleAction(
            actionType,
            grappleDamageModeOverride,
            opponentWeaponHandSlotOverride,
            usesIterativeAttack ? attackBonusUsed : (int?)null);

        FinalizeGrappleActionResolution(actor, actionType, directResult, isFreeAction, usesIterativeAttack, attackBonusUsed, attacksRemaining);
    }

    private IEnumerator AI_GrappleRestrictedTurn(CharacterController npc)
    {
        if (npc == null || npc.Stats == null)
            yield break;

        const int maxAttemptsPerTurn = 8;
        int attempts = 0;

        while (attempts < maxAttemptsPerTurn)
        {
            attempts++;

            if (!npc.TryGetGrappleState(out CharacterController opponent, out _, out bool actorPinned, out bool opponentPinned) || opponent == null || opponent.Stats == null)
            {
                CombatUI?.ShowCombatLog($"⚠ {npc.Stats.CharacterName} is in an invalid grapple state and cannot pick a grapple action.");
                yield return new WaitForSeconds(0.35f);
                yield break;
            }

            GrappleActionType? chosenAction = ChooseNPCGrappleAction(npc, opponent, actorPinned, opponentPinned);
            if (chosenAction == null)
            {
                CombatUI?.ShowCombatLog($"⚠ {npc.Stats.CharacterName} has no legal grapple actions available.");
                yield return new WaitForSeconds(0.35f);
                yield break;
            }

            bool isFreeAction = chosenAction.Value == GrappleActionType.ReleasePinnedOpponent;
            bool usesIterativeAttack = CharacterController.IsIterativeGrappleAttackAction(chosenAction.Value);
            int? iterativeAttackBonusOverride = null;
            int remainingAttacks = 0;

            if (usesIterativeAttack)
            {
                if (!TryConsumeIterativeGrappleAttack(npc, chosenAction.Value, out int attackBonusUsed, out remainingAttacks))
                    break;

                iterativeAttackBonusOverride = attackBonusUsed;
            }
            else if (!isFreeAction && !npc.CommitStandardAction())
            {
                CombatUI?.ShowCombatLog($"⚠ {npc.Stats.CharacterName} cannot use grapple action: standard action already spent.");
                break;
            }

            SpecialAttackResult result = null;
            bool usedEscapePrompt = false;
            if (chosenAction.Value == GrappleActionType.OpposedGrappleEscape)
            {
                usedEscapePrompt = TryResolveOpposedGrappleEscapeWithPrompt(
                    npc,
                    iterativeAttackBonusOverride,
                    onResolved: resolved => result = resolved);
            }

            if (!usedEscapePrompt)
            {
                AttackDamageMode? grappleDamageModeOverride = ShouldForceLethalGrappleDamageForNPC(npc, chosenAction.Value)
                    ? AttackDamageMode.Lethal
                    : null;
                result = npc.ResolveGrappleAction(chosenAction.Value, grappleDamageModeOverride, null, iterativeAttackBonusOverride);
            }
            else
            {
                yield return new WaitUntil(() => result != null);
            }

            string iterativeLabel = usesIterativeAttack
                ? $" [BAB {CharacterStats.FormatMod(iterativeAttackBonusOverride ?? 0)} | {remainingAttacks} left]"
                : string.Empty;
            string npcGrappleHeader = $"☠ {npc.Stats.CharacterName} chooses grapple action [{chosenAction.Value}]{iterativeLabel} against {opponent.Stats.CharacterName}";
            if (!string.IsNullOrEmpty(result.Log) && result.Log.Contains("\n"))
            {
                CombatUI?.ShowCombatLog(npcGrappleHeader);
                CombatUI?.ShowCombatLog(result.Log);
            }
            else
            {
                CombatUI?.ShowCombatLog($"{npcGrappleHeader}: {result.Log}");
            }
            UpdateAllStatsUI();

            TryOfferFreeAdjacentMovementAfterGrappleEnds(npc, chosenAction.Value, result);

            if (result != null && result.TargetKilled)
            {
                HandleSummonDeathCleanup(opponent);

                if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI?.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI?.SetActionButtonsVisible(false);
                    yield break;
                }

                break;
            }

            float pause = isFreeAction ? 0.35f : (usesIterativeAttack ? 0.45f : 0.8f);
            yield return new WaitForSeconds(pause);

            // Non-iterative grapple actions end AI grapple sequence for the turn.
            if (!usesIterativeAttack)
                break;

            if (remainingAttacks <= 0)
                break;

            if (UnityEngine.Random.value < 0.2f)
                break;
        }
    }

    private GrappleActionType? ChooseNPCGrappleAction(CharacterController npc, CharacterController opponent, bool actorPinned, bool opponentPinned)
    {
        List<GrappleActionType> legalActions = BuildNPCLegalGrappleActions(npc, opponent, actorPinned, opponentPinned);
        if (legalActions.Count == 0)
            return null;

        if (TryChoosePredatoryAnimalGrappleAction(npc, legalActions, actorPinned, opponentPinned, out GrappleActionType animalAction))
            return animalAction;

        if (actorPinned)
        {
            if (legalActions.Contains(GrappleActionType.BreakPin))
                return GrappleActionType.BreakPin;
            if (legalActions.Contains(GrappleActionType.OpposedGrappleEscape))
                return GrappleActionType.OpposedGrappleEscape;
            if (legalActions.Contains(GrappleActionType.EscapeArtist))
                return GrappleActionType.EscapeArtist;
        }

        bool isPinning = npc.IsPinningOpponent();
        if (isPinning)
        {
            CharacterController pinnedTarget = npc.GetPinnedOpponent();

            if (pinnedTarget != null && pinnedTarget.Stats != null && pinnedTarget.Stats.CurrentHP > 0 && legalActions.Contains(GrappleActionType.DamageOpponent))
                return GrappleActionType.DamageOpponent;

            if (legalActions.Contains(GrappleActionType.UseOpponentWeapon))
                return GrappleActionType.UseOpponentWeapon;

            if (legalActions.Contains(GrappleActionType.DisarmSmallObject) && UnityEngine.Random.value < 0.5f)
                return GrappleActionType.DisarmSmallObject;

            if (legalActions.Contains(GrappleActionType.MoveHalfSpeed) && UnityEngine.Random.value < 0.2f)
                return GrappleActionType.MoveHalfSpeed;

            if (legalActions.Contains(GrappleActionType.ReleasePinnedOpponent) && UnityEngine.Random.value < 0.05f)
                return GrappleActionType.ReleasePinnedOpponent;

            if (legalActions.Contains(GrappleActionType.DamageOpponent))
                return GrappleActionType.DamageOpponent;
        }
        else
        {
            if (!opponentPinned && legalActions.Contains(GrappleActionType.PinOpponent) && npc.Stats.STRMod >= 3 && UnityEngine.Random.value < 0.35f)
                return GrappleActionType.PinOpponent;

            if (legalActions.Contains(GrappleActionType.AttackWithLightWeapon) && UnityEngine.Random.value < 0.45f)
                return GrappleActionType.AttackWithLightWeapon;

            if (legalActions.Contains(GrappleActionType.UseOpponentWeapon) && UnityEngine.Random.value < 0.30f)
                return GrappleActionType.UseOpponentWeapon;

            if (legalActions.Contains(GrappleActionType.AttackUnarmed) && UnityEngine.Random.value < 0.20f)
                return GrappleActionType.AttackUnarmed;

            if (legalActions.Contains(GrappleActionType.DamageOpponent))
                return GrappleActionType.DamageOpponent;
        }

        return legalActions[UnityEngine.Random.Range(0, legalActions.Count)];
    }

    private static bool IsPredatoryAnimalGrapplerProfile(CharacterController npc, out AnimalAIProfile animalProfile)
    {
        animalProfile = npc != null ? npc.aiProfile as AnimalAIProfile : null;
        return animalProfile != null && animalProfile.ShouldPrioritizeLethalNaturalGrappleAttacks(npc);
    }

    private bool ShouldForceLethalGrappleDamageForNPC(CharacterController npc, GrappleActionType actionType)
    {
        if (actionType != GrappleActionType.DamageOpponent)
            return false;

        return IsPredatoryAnimalGrapplerProfile(npc, out _);
    }

    private bool TryChoosePredatoryAnimalGrappleAction(
        CharacterController npc,
        List<GrappleActionType> legalActions,
        bool actorPinned,
        bool opponentPinned,
        out GrappleActionType chosenAction)
    {
        chosenAction = default;

        if (npc == null || npc.Stats == null)
            return false;

        if (!(npc.aiProfile is AnimalAIProfile animalProfile))
            return false;

        if (animalProfile.ShouldAttemptEmergencyGrappleEscape(npc))
        {
            if (legalActions.Contains(GrappleActionType.BreakPin))
            {
                chosenAction = GrappleActionType.BreakPin;
                return true;
            }

            if (legalActions.Contains(GrappleActionType.OpposedGrappleEscape))
            {
                chosenAction = GrappleActionType.OpposedGrappleEscape;
                return true;
            }

            if (legalActions.Contains(GrappleActionType.EscapeArtist))
            {
                chosenAction = GrappleActionType.EscapeArtist;
                return true;
            }

            return false;
        }

        if (!animalProfile.ShouldPrioritizeLethalNaturalGrappleAttacks(npc))
            return false;

        if (actorPinned)
        {
            if (legalActions.Contains(GrappleActionType.BreakPin))
            {
                chosenAction = GrappleActionType.BreakPin;
                return true;
            }

            if (legalActions.Contains(GrappleActionType.OpposedGrappleEscape))
            {
                chosenAction = GrappleActionType.OpposedGrappleEscape;
                return true;
            }

            if (legalActions.Contains(GrappleActionType.EscapeArtist))
            {
                chosenAction = GrappleActionType.EscapeArtist;
                return true;
            }

            return false;
        }

        if (npc.IsPinningOpponent() && legalActions.Contains(GrappleActionType.ReleasePinnedOpponent))
        {
            chosenAction = GrappleActionType.ReleasePinnedOpponent;
            return true;
        }

        if (!opponentPinned && legalActions.Contains(GrappleActionType.AttackUnarmed))
        {
            int naturalAttackCount = 0;
            List<NaturalAttackDefinition> naturalAttacks = npc.Stats.GetValidNaturalAttacks();
            if (naturalAttacks != null)
            {
                for (int i = 0; i < naturalAttacks.Count; i++)
                    naturalAttackCount += Mathf.Max(1, naturalAttacks[i].Count);
            }

            int rakeAttackCount = 0;
            if (npc.Stats.HasRake)
            {
                NaturalAttackDefinition rakeAttack = npc.Stats.GetRakeAttackDefinition();
                rakeAttackCount = Mathf.Max(1, rakeAttack != null ? rakeAttack.Count : 2);
            }

            Debug.Log($"[AI][Grapple] {npc.Stats.CharacterName} chooses full natural grapple routine: natural={naturalAttackCount}, rake={rakeAttackCount}, total={naturalAttackCount + rakeAttackCount}.");

            chosenAction = GrappleActionType.AttackUnarmed;
            return true;
        }

        if (legalActions.Contains(GrappleActionType.DamageOpponent))
        {
            chosenAction = GrappleActionType.DamageOpponent;
            return true;
        }

        return false;
    }

    private List<GrappleActionType> BuildNPCLegalGrappleActions(CharacterController npc, CharacterController opponent, bool actorPinned, bool opponentPinned)
    {
        var actions = new List<GrappleActionType>();
        if (npc == null || opponent == null)
            return actions;

        bool hasStandardAction = npc.Actions != null && npc.Actions.HasStandardAction;

        void TryAdd(GrappleActionType actionType, bool requiresStandardAction = true)
        {
            if (CharacterController.IsIterativeGrappleAttackAction(actionType))
            {
                if (!CanUseGrappleAttackOption(npc))
                    return;
            }
            else if (requiresStandardAction && !hasStandardAction)
            {
                return;
            }

            if (npc.IsGrappleActionBlockedWhilePinning(actionType, out _))
                return;

            switch (actionType)
            {
                case GrappleActionType.BreakPin:
                    if (!actorPinned)
                        return;
                    break;

                case GrappleActionType.DamageOpponent:
                case GrappleActionType.AttackWithLightWeapon:
                case GrappleActionType.AttackUnarmed:
                case GrappleActionType.PinOpponent:
                case GrappleActionType.MoveHalfSpeed:
                case GrappleActionType.UseOpponentWeapon:
                case GrappleActionType.DisarmSmallObject:
                    if (actorPinned)
                        return;
                    break;

                case GrappleActionType.ReleasePinnedOpponent:
                    if (!npc.IsPinningOpponent() || !opponentPinned)
                        return;
                    break;
            }

            if (actionType == GrappleActionType.UseOpponentWeapon)
            {
                List<DisarmableHeldItemOption> opponentLightWeapons = opponent.GetEquippedLightHandWeaponOptions();
                if (opponentLightWeapons == null || opponentLightWeapons.Count == 0)
                    return;
            }

            if (actionType == GrappleActionType.AttackWithLightWeapon)
            {
                if (!npc.CanAttackWithLightWeaponWhileGrappling(out _, out _))
                    return;
            }

            if (actionType == GrappleActionType.AttackUnarmed)
            {
                if (!npc.CanAttackUnarmedWhileGrappling(out _))
                    return;
            }
            actions.Add(actionType);
        }

        if (actorPinned)
        {
            TryAdd(GrappleActionType.OpposedGrappleEscape);
            TryAdd(GrappleActionType.EscapeArtist);
            return actions;
        }

        if (npc.IsPinningOpponent())
        {
            TryAdd(GrappleActionType.DamageOpponent);
            TryAdd(GrappleActionType.UseOpponentWeapon);
            TryAdd(GrappleActionType.MoveHalfSpeed);
            TryAdd(GrappleActionType.DisarmSmallObject);
            TryAdd(GrappleActionType.ReleasePinnedOpponent, requiresStandardAction: false);
            return actions;
        }

        TryAdd(GrappleActionType.DamageOpponent);
        TryAdd(GrappleActionType.AttackWithLightWeapon);
        TryAdd(GrappleActionType.AttackUnarmed);
        TryAdd(GrappleActionType.PinOpponent);
        TryAdd(GrappleActionType.UseOpponentWeapon);
        TryAdd(GrappleActionType.MoveHalfSpeed);
        TryAdd(GrappleActionType.BreakPin);
        TryAdd(GrappleActionType.OpposedGrappleEscape);
        TryAdd(GrappleActionType.EscapeArtist);
        TryAdd(GrappleActionType.ReleasePinnedOpponent, requiresStandardAction: false);

        return actions;
    }
}
