using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    public bool CanUseTemplateSmite(CharacterController actor, out string reason)
    {
        reason = string.Empty;

        if (actor == null || actor.Stats == null || actor.Actions == null)
        {
            reason = "No active character";
            return false;
        }

        bool hasSmite = actor.Stats.HasTemplateSmiteEvil || actor.Stats.HasTemplateSmiteGood;
        if (!hasSmite)
        {
            reason = "No smite ability";
            return false;
        }

        if (!actor.Actions.HasStandardAction)
        {
            reason = "Standard action used";
            return false;
        }

        if (actor.Stats.TemplateSmiteUsed)
        {
            reason = "Already used";
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Turned))
        {
            reason = "Turned condition";
            return false;
        }

        if (!HasAnyValidTemplateSmiteTarget(actor))
        {
            reason = "No valid alignment target in range";
            return false;
        }

        return true;
    }

    public void OnSmiteButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "smite"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "smite"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!CanUseTemplateSmite(pc, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot smite: {reason}.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        EndAttackSequence();
        _pendingAttackMode = PendingAttackMode.TemplateSmite;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowTemplateSmiteTargets(pc);

        string axisLabel = GetTemplateSmiteAxisLabel(pc);
        CombatUI?.SetTurnIndicator($"SMITE {axisLabel.ToUpperInvariant()}: Select a highlighted {axisLabel.ToLowerInvariant()} enemy in range");
    }

    private bool HasAnyValidTemplateSmiteTarget(CharacterController attacker)
    {
        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (IsValidTemplateSmiteTarget(attacker, candidate))
                return true;
        }

        return false;
    }

    private bool IsValidTemplateSmiteTarget(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null)
            return false;

        if (attacker == target || target.Stats.IsDead)
            return false;

        if (!IsEnemyTeam(attacker, target))
            return false;

        bool smitesEvil = attacker.Stats.HasTemplateSmiteEvil;
        bool smitesGood = attacker.Stats.HasTemplateSmiteGood;

        bool validAlignment = (smitesEvil && AlignmentHelper.IsEvil(target.Stats.CharacterAlignment))
            || (smitesGood && AlignmentHelper.IsGood(target.Stats.CharacterAlignment));

        if (!validAlignment)
            return false;

        return attacker.IsTargetInCurrentWeaponRange(target);
    }

    private string GetTemplateSmiteAxisLabel(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return "Evil";

        if (attacker.Stats.HasTemplateSmiteGood && !attacker.Stats.HasTemplateSmiteEvil)
            return "Good";

        return "Evil";
    }

    private void ShowTemplateSmiteTargets(CharacterController attacker)
    {
        if (attacker == null || Grid == null)
            return;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        HighlightCharacterFootprint(attacker, HighlightType.Selected);

        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController target = all[i];
            if (!IsValidTemplateSmiteTarget(attacker, target))
                continue;

            List<Vector2Int> occupied = target.GetOccupiedSquares();
            for (int s = 0; s < occupied.Count; s++)
            {
                SquareCell cell = Grid.GetCell(occupied[s]);
                if (cell == null)
                    continue;

                cell.SetHighlight(HighlightType.Attack);
                if (!_highlightedCells.Contains(cell))
                    _highlightedCells.Add(cell);
            }
        }

        if (_highlightedCells.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no valid alignment target in weapon range for smite.");
            ShowActionChoices();
        }
    }

    private void ExecuteTemplateSmiteAttack(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
            return;

        if (!CanUseTemplateSmite(attacker, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot smite: {reason}.");
            ShowActionChoices();
            return;
        }

        if (!IsValidTemplateSmiteTarget(attacker, target))
        {
            CombatUI?.ShowCombatLog("⚠ Invalid smite target.");
            ShowTemplateSmiteTargets(attacker);
            return;
        }

        if (!attacker.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no standard action available.");
            ShowActionChoices();
            return;
        }

        int attackBonus = attacker.Stats.CHAMod;
        int hitDice = Mathf.Max(1, attacker.Stats.HitDice > 0 ? attacker.Stats.HitDice : attacker.Stats.Level);
        int damageBonus = hitDice;

        attacker.Stats.MoraleAttackBonus += attackBonus;
        attacker.Stats.MoraleDamageBonus += damageBonus;

        CombatResult result;
        try
        {
            List<CharacterController> allCombatants = GetAllCharacters();
            bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allCombatants, out CharacterController flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null && flankPartner.Stats != null ? flankPartner.Stats.CharacterName : string.Empty;
            RangeInfo rangeInfo = _combatFlowService != null ? _combatFlowService.CalculateRangeInfo(attacker, target) : null;
            result = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        }
        finally
        {
            attacker.Stats.MoraleAttackBonus -= attackBonus;
            attacker.Stats.MoraleDamageBonus -= damageBonus;
        }

        attacker.Stats.TemplateSmiteUsed = true;

        string axisLabel = GetTemplateSmiteAxisLabel(attacker);
        CombatUI?.ShowCombatLog($"<color=#FFD280>✦ {attacker.Stats.CharacterName} uses Smite {axisLabel}! (+{attackBonus} attack, +{damageBonus} damage)</color>");
        if (result != null)
            CombatUI?.ShowCombatLog(result.GetDetailedSummary());

        if (result != null && result.Hit && result.TotalDamage > 0)
            Combat_CheckConcentrationOnDamage(target, result.TotalDamage);

        if (result != null && result.TargetKilled)
        {
            Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy && Combat_AreAllNPCsDead())
            {
                Combat_SetPhase(TurnPhase.CombatOver);
                CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
                CombatUI?.SetActionButtonsVisible(false);
            }
        }

        Combat_SetLastCombatLog(result != null ? result.GetDetailedSummary() : string.Empty);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (CurrentPhase != TurnPhase.CombatOver)
            Combat_StartAfterAttackDelay(attacker, 1.0f);
    }
}
