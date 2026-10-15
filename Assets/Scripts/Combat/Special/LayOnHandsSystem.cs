using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The PC Lay on Hands button and the AI's lay on hands: thin wrappers over the one rule, LayOnHandsRules.Resolve
/// (PHB p.44; CHR-020). The PC presses the button and clicks itself, an ally within touch or an undead creature within
/// reach; the AI picks with AIProfile.ChooseLayOnHandsTarget. Both log the same line and check the combat end.
/// </summary>
public partial class GameManager
{
    /// <summary>Whether the Lay on Hands button is usable now, with any valid target within touch.</summary>
    public bool CanUseLayOnHandsButton(CharacterController actor, out string reason)
    {
        if (!LayOnHandsRules.CanUse(actor, out reason))
            return false;
        if (actor.HasCondition(CombatConditionType.Turned))
        {
            reason = "Turned condition";
            return false;
        }
        if (!HasAnyValidLayOnHandsTarget(actor))
        {
            reason = "No one to touch";
            return false;
        }
        return true;
    }

    private bool HasAnyValidLayOnHandsTarget(CharacterController actor)
    {
        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            if (LayOnHandsRules.IsValidTarget(actor, all[i], out _, out _))
                return true;
        }
        return false;
    }

    /// <summary>The PC Lay on Hands button: highlights the creatures within touch the paladin may heal or harm.</summary>
    public void OnLayOnHandsButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null)
            return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "lay on hands"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "lay on hands"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!CanUseLayOnHandsButton(pc, out string reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} cannot lay on hands: {reason}."));
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        EndAttackSequence();
        _pendingAttackMode = PendingAttackMode.LayOnHands;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowLayOnHandsTargets(pc);
        CombatUI?.SetTurnIndicator($"LAY ON HANDS ({LayOnHandsRules.RemainingPool(pc.Stats)} points left): click yourself, a hurt ally within touch or an undead foe in reach");
    }

    private void ShowLayOnHandsTargets(CharacterController actor)
    {
        if (actor == null || Grid == null)
            return;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController target = all[i];
            if (!LayOnHandsRules.IsValidTarget(actor, target, out bool harms, out _))
                continue;
            HighlightCharacterFootprint(target, harms ? HighlightType.Attack : HighlightType.Selected, addToSelectableCells: true);
        }

        if (_highlightedCells.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{actor.Stats.CharacterName} has no one within touch to lay on hands."));
            _pendingAttackMode = PendingAttackMode.Single;
            ShowActionChoices();
        }
    }

    /// <summary>
    /// The PC's click while choosing the Lay on Hands target; a click off the highlighted squares cancels. Every exit
    /// that leaves target selection resets the pending attack mode, so a later Break Wall (or other) target click is not
    /// routed here.
    /// </summary>
    private void HandleLayOnHandsTargetClick(CharacterController pc, SquareCell cell)
    {
        if (pc == null || cell == null)
            return;

        if (!_highlightedCells.Contains(cell))
        {
            _pendingAttackMode = PendingAttackMode.Single;
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Lay on Hands cancelled."));
            Grid?.ClearAllHighlights();
            _highlightedCells.Clear();
            ShowActionChoices();
            return;
        }

        // A square of the paladin's own is itself while it may heal itself; otherwise the creature in the square the
        // paladin may touch, a square-mate included (CMB-131: a square shared by two creatures yields the one the rules
        // accept).
        bool onSelf = cell.Coords == pc.GridPosition || pc.GetOccupiedSquares().Contains(cell.Coords);
        CharacterController target = onSelf && LayOnHandsRules.IsValidTarget(pc, pc, out _, out _)
            ? pc
            : cell.GetOccupantOtherThan(pc, c => LayOnHandsRules.IsValidTarget(pc, c, out _, out _));
        if (target == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Select a highlighted creature within touch."));
            return;
        }

        Grid?.ClearAllHighlights();
        _highlightedCells.Clear();
        _pendingAttackMode = PendingAttackMode.Single;

        if (!ExecuteLayOnHands(pc, target))
        {
            ShowActionChoices();
            return;
        }

        if (CurrentPhase != TurnPhase.CombatOver)
            StartCoroutine(AfterAttackDelay(pc, 0.6f));
    }

    /// <summary>
    /// The shared executor behind the PC click and the AI: LayOnHandsRules.Resolve, the combat log, the stats UI and the
    /// combat-end check. Returns true when the use happened (the standard action is spent).
    /// </summary>
    private bool ExecuteLayOnHands(CharacterController actor, CharacterController target, int? requestedPoints = null)
    {
        LayOnHandsResult result = LayOnHandsRules.Resolve(actor, target, requestedPoints);
        if (!result.Performed)
        {
            string actorName = actor != null && actor.Stats != null ? actor.Stats.CharacterName : "?";
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{actorName} cannot lay on hands: {result.Refusal}."));
            return false;
        }

        CombatUI?.ShowCombatLog(result.HarmsUndead
            ? CombatLogHelper.Color($"✋ {result.Log}", "FFD280")
            : CombatLogHelper.Buff("✋", result.Log));

        // The damage went through DealDamage, which ran the death handling (OnDeath, summon cleanup).
        UpdateAllStatsUI();
        EvaluateCombatEnd("LayOnHands.Resolve");
        return true;
    }

    /// <summary>The AI (and scenario) entry: lays on hands on <paramref name="target"/> through the shared executor.</summary>
    public bool TryLayOnHandsForAI(CharacterController npc, CharacterController target, int? requestedPoints = null)
        => ExecuteLayOnHands(npc, target, requestedPoints);

    /// <summary>
    /// The AI's lay on hands at the start of its action: when its profile picks a target (AIProfile.ChooseLayOnHandsTarget)
    /// the creature lays on hands with its standard action. Returns true when it did.
    /// </summary>
    public bool TryNPCLayOnHandsTurnForAI(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || !LayOnHandsRules.HasAbility(npc.Stats))
            return false;
        if (!LayOnHandsRules.CanUse(npc, out _))
            return false;

        List<CharacterController> all = GetAllCharacters();
        CharacterController target = npc.aiProfile != null
            ? npc.aiProfile.ChooseLayOnHandsTarget(npc, all)
            : DND35.AI.AIProfile.DefaultLayOnHandsTarget(npc, all);
        if (target == null)
            return false;
        return ExecuteLayOnHands(npc, target);
    }
}
