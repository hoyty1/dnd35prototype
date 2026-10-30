using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager partial class: the cores of the class-ability entries (batch 13 step 16, D9, D10): turn undead, rage and
/// the activated domain powers, moved out of the PC-bound button code (the old ExecuteTurnUndead with its target menu,
/// OnRageButtonPressed's body, ActivateDomainPowerByName). Actor-agnostic: no UI follow-up here (the PC runner's UiAfter
/// does it) and no read of who controls the actor; ActionCatalogTests scans this file with the executors.
/// </summary>
public partial class GameManager
{
    // ── Turn undead (D9; PHB p.159) ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <paramref name="turner"/> turns undead (PHB p.159, D9). Refused with nothing spent when it may not
    /// (<see cref="CanUseTurnUndead"/>). Otherwise it pays first, the standard action and the day's attempt, then rolls the
    /// turning check (d20 + Cha, +2 with 5 ranks in Knowledge (religion), PHB p.33; Table 8-9 gives the most Hit Dice it
    /// affects). The candidates are the undead within 60 ft, whatever their side, with line of effect (<see cref="GetTurnUndeadCandidates"/>; one behind a wall is not affected, CMB-026), closest
    /// first, ties to the lower HD; already turned ones are skipped unless <paramref name="includeTurned"/> (the turner's
    /// choice; the AI skips them). Only when the check can affect at least one of them is the turning damage rolled (2d6 +
    /// turning level + Cha: the pool of Hit Dice). The pool takes them in order and stops at the first that does not fit
    /// (delegated RAW reading 2026-10-29: the text allows only the skip over already turned undead); an undead of half the
    /// turning level or less is destroyed (PHB p.159), every affected one under a greater turning (Sun domain, PHB p.188),
    /// the rest are turned for 10 rounds. An undead whose HD exceed the check's result is not turnable on this attempt,
    /// so it is passed over without stopping the pool (delegated RAW reading 2026-10-29: "the closest turnable undead").
    /// </summary>
    internal void ActionCore_TurnUndead(CharacterController turner, bool includeTurned, ActionOutcome o)
    {
        if (turner == null || turner.Stats == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no actor"));
            return;
        }
        if (!CanUseTurnUndead(turner, out string reason))
        {
            o.Refuse(Legality.Refused(TurnUndeadRefusalBar(reason), reason));
            return;
        }
        int level = GetTurnUndeadEffectiveLevel(turner);
        if (level <= 0)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no turning level"));
            return;
        }

        // Pay first (the action and the day's attempt), then roll.
        if (!turner.CommitStandardAction())
        {
            o.Refuse(Legality.Refused(LegalityBar.Action, "no standard action left"));
            return;
        }
        turner.Stats.TurnUndeadAttemptsUsedToday++;
        o.Performed = true;
        int attemptsLeft = GetRemainingTurnUndeadAttempts(turner);
        string name = turner.Stats.CharacterName;
        int cha = turner.Stats.CHAMod;
        bool greater = turner.Stats.GreaterTurningActive;

        CombatUI?.ShowCombatLog(CombatLogHelper.Death("✝", $"️ {name} invokes {(greater ? "Greater Turning" : "Turn Undead")}! (Attempt {turner.Stats.TurnUndeadAttemptsUsedToday}/{turner.Stats.MaxTurnUndeadAttemptsPerDay})"));

        int checkRoll = DiceService.D20("Turning check");
        // +2 with 5 ranks in Knowledge (religion) (PHB p.33, p.78, p.159).
        int synergy = TurningCheckBonusAgainstUndead(turner);
        int checkTotal = checkRoll + cha + synergy;
        int maxHd = ComputeTurnUndeadMaxHitDice(checkTotal, level);
        string synergyText = synergy != 0 ? $" + Knowledge (religion) {CharacterStats.FormatMod(synergy)}" : string.Empty;
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   Turning Check: d20 ({checkRoll}) + CHA {CharacterStats.FormatMod(cha)}{synergyText} = {checkTotal} → affects undead up to {maxHd} HD"));

        List<CharacterController> candidates = GetTurnUndeadCandidates(turner, out List<CharacterController> blocked);
        foreach (CharacterController undead in blocked)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   {undead.Stats.CharacterName} has no line of effect from {name} (total cover): not affected."));

        // The turnable undead of this attempt, in order: the check's limit, and the skip over already turned ones.
        var order = new List<CharacterController>();
        foreach (CharacterController undead in candidates)
        {
            int hd = undead.Stats.GetHitDice();
            if (!includeTurned && IsTurnedUndead(undead))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   {undead.Stats.CharacterName} is already turned: skipped."));
                continue;
            }
            if (hd > maxHd)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   {undead.Stats.CharacterName} ({hd} HD) is too powerful to be turned."));
                continue;
            }
            order.Add(undead);
        }

        int turned = 0, destroyed = 0, pool = 0;
        if (order.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "   No undead within 60 ft can be affected."));
        }
        else
        {
            // Turning damage only when the check can affect some undead in range (PHB p.159).
            int damageRoll = DiceService.RollMultiple(2, 6, "Turning damage");
            pool = Mathf.Max(0, damageRoll + level + cha);
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   Turning Damage: 2d6 ({damageRoll}) + turning level {level} + CHA {CharacterStats.FormatMod(cha)} = {pool} total HD"));

            for (int i = 0; i < order.Count; i++)
            {
                CharacterController undead = order[i];
                int hd = undead.Stats.GetHitDice();
                if (hd > pool)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   {undead.Stats.CharacterName} ({hd} HD) needs more than the {pool} HD left: the turning stops here."));
                    break;
                }
                pool -= hd;

                bool canDestroy = level >= hd * 2;
                if (canDestroy || greater)
                {
                    int lethalDamage = Mathf.Max(1, undead.Stats.CurrentHP + 10);
                    undead.Stats.TakeDamage(lethalDamage);
                    HandleSummonDeathCleanup(undead);
                    destroyed++;
                    string source = greater && !canDestroy ? "Greater Turning" : "holy power";
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   💥 {undead.Stats.CharacterName} is destroyed by {source}! ({hd} HD)"));
                }
                else
                {
                    if (_conditionService != null)
                        _conditionService.ApplyCondition(undead, CombatConditionType.Turned, 10, turner);
                    else
                        undead.ApplyCondition(CombatConditionType.Turned, 10, name);

                    RegisterTurnUndeadTracker(undead, turner);
                    turned++;
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   ↩ {undead.Stats.CharacterName} is turned for 10 rounds and flees! ({hd} HD)"));
                }
            }

            if (turned == 0 && destroyed == 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "   The divine surge fails to overcome any undead this turn."));
        }

        // A greater turning is spent with the attempt it replaces (PHB p.188).
        if (greater)
            turner.Stats.GreaterTurningActive = false;

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   Results: {destroyed} destroyed, {turned} turned, {pool} HD turning power unspent."));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   Remaining Turn Undead attempts today: {attemptsLeft}"));
        o.CoreOutcome = turned + destroyed;

        UpdateAllStatsUI();

        // Destroyed undead may be the last enemies (CORE-011).
        if (destroyed > 0)
            EvaluateCombatEnd("TurnUndead.Resolve");
    }

    // ── Rage (D10; PHB p.25) ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <paramref name="actor"/> enters a barbarian rage (PHB p.25-26): no action, only on its own turn
    /// (RageActionExecutor.RefusalOf; refused with nothing spent otherwise), through CharacterController.ActivateRage (+4
    /// Strength and Constitution, +2 Will, -2 AC; greater and mighty rage more).
    /// </summary>
    internal void ActionCore_Rage(CharacterController actor, ActionOutcome o)
    {
        string reason = RageActionExecutor.RefusalOf(actor, out LegalityBar bar);
        if (reason != null)
        {
            o.Refuse(Legality.Refused(bar, reason));
            return;
        }
        if (!actor.ActivateRage())
        {
            o.Refuse(Legality.Refused(LegalityBar.Resource, "cannot rage now"));
            return;
        }
        o.Performed = true;
        CharacterStats s = actor.Stats;
        int bonus = s.RageAbilityBonus;
        CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡",
            $"{s.CharacterName} enters a BARBARIAN RAGE! +{bonus} STR, +{bonus} CON, +{bonus / 2} Will, -2 AC for {s.RageRoundsRemaining} rounds!"));
        UpdateAllStatsUI();
    }

    // ── Domain powers (PHB p.185-189) ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <paramref name="cleric"/> uses the activated granted power of <paramref name="domain"/> (DomainPowerActionExecutor):
    /// the power's own method (its use, its action, its log), a Death touch on <paramref name="target"/>, and for Sun a
    /// greater turning: the flag, then the turn-undead core in the same action (PHB p.188: in place of a regular turning
    /// attempt). Performed when the power was used.
    /// </summary>
    internal void ActionCore_DomainPower(CharacterController cleric, string domain, CharacterController target, bool includeTurned, ActionOutcome o)
    {
        if (cleric == null || cleric.Stats == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no actor"));
            return;
        }
        if (!CanActivateDomainPower(cleric, domain, out string reason))
        {
            o.Refuse(Legality.Refused(LegalityBar.Resource, reason));
            return;
        }

        bool used;
        switch (domain)
        {
            case "Strength": used = ActivateStrengthDomain(cleric); break;
            case "Destruction": used = ActivateDestructionSmite(cleric); break;
            case "Death": used = ActivateDeathTouch(cleric, target); break;
            case "Travel": used = ActivateTravelFreedom(cleric); break;
            case "Plant": used = ActivatePlantRebuke(cleric); break;
            case "Luck": used = ActivateLuckReroll(cleric); break;
            case "Air":
            case "Earth":
            case "Fire":
            case "Water":
                used = ActivateElementalTurning(cleric, domain);
                break;
            case "Sun":
                if (!CanUseTurnUndead(cleric, out string turnReason))
                {
                    o.Refuse(Legality.Refused(TurnUndeadRefusalBar(turnReason), turnReason));
                    return;
                }
                used = ActivateGreaterTurning(cleric);
                if (used)
                {
                    ActionCore_TurnUndead(cleric, includeTurned, o);
                    // A turning refused after all leaves the day's greater turning unspent.
                    if (!o.Performed && cleric.Stats.GreaterTurningActive)
                    {
                        cleric.Stats.GreaterTurningActive = false;
                        cleric.Stats.SunDomainUsesToday = Mathf.Max(0, cleric.Stats.SunDomainUsesToday - 1);
                    }
                    return;
                }
                break;
            default:
                o.Refuse(Legality.Refused(LegalityBar.Target, domain + " domain has no activated power"));
                return;
        }

        if (used)
            o.Performed = true;
        else
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, domain + " domain power was not used"));
            o.RefusalLogged = true; // the power's method logged why
        }
        UpdateAllStatsUI();
        // A death touch or an elemental turning may slay or destroy the last foe (CORE-011).
        bool mayKill = domain == "Death" || domain == "Air" || domain == "Earth" || domain == "Fire" || domain == "Water";
        if (used && mayKill && CurrentPhase != TurnPhase.CombatOver)
            EvaluateCombatEnd("DomainPower." + domain);
    }
}
