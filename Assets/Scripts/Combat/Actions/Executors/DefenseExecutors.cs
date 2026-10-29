using System.Collections;

/// <summary>
/// The total defense rule (PHB p.142; CMB-179, batch 13 step 4, decision D2, docs/systems/RULES_COVERAGE.md 4.1), one rule
/// for every creature:
/// - a standard action that does not provoke (Table 8-2);
/// - a +4 dodge bonus to AC from the moment it is taken until the start of the creature's next turn, +6 with 5 or more
///   ranks in Tumble (PHB p.85, Tumble's synergy); as a dodge bonus it goes with the Dexterity bonus to AC (PHB p.136,
///   CharacterStats.DodgeACBonus);
/// - not with fighting defensively or Combat Expertise: both are declared with an attack (PHB p.142), so only a creature
///   that attacked with either this turn is refused (it has no standard action left anyway); a fighting-defensively flag
///   set without an attack (the cornered frightened creature's stance, AI-044; a PC's abandoned Attack Defensively
///   targeting) is no declaration, and total defense clears it so the two bonuses never stack;
/// - no attacks of opportunity while it lasts (ThreatSystem.CanMakeAoO);
/// - not while grappling: a grappling creature takes only the grapple actions (PHB p.156);
/// - not by a creature whose condition takes its own choice of action away: one that can take no action at all
///   (ConditionRules.DeniesAllActions: cowering, fascinated, stunned and the rest), a turned creature (it flees, or cowers
///   when it cannot, PHB p.159) and a panicked one, except when it is cornered (it cowers, typically using total defense,
///   PHB p.311; TurnStartState.CorneredInPanic, set only by FrightenedBehaviorController.ExecuteDecision; D2).
/// </summary>
public static class TotalDefenseRules
{
    public const int DodgeBonusBase = 4;
    public const int DodgeBonusWithTumble = 6;
    public const int TumbleRanksForSynergy = 5;

    /// <summary>The dodge bonus total defense gives <paramref name="stats"/>: +4, or +6 with 5 or more ranks in Tumble (PHB p.142, p.85).</summary>
    public static int DodgeBonus(CharacterStats stats)
    {
        if (stats == null)
            return DodgeBonusBase;
        int tumbleRanks = stats.Skills != null && stats.Skills.TryGetValue("Tumble", out Skill tumble) && tumble != null ? tumble.Ranks : 0;
        return tumbleRanks >= TumbleRanksForSynergy ? DodgeBonusWithTumble : DodgeBonusBase;
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may take total defense now: alive and in the fight, no condition that takes its
    /// choice of action away (the cornered panicked creature excepted), not grappling, a standard action left, and no
    /// attack made fighting defensively or with Combat Expertise this turn. Pure: no dice, no log, no state change.
    /// </summary>
    public static Legality Check(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        if (ConditionRules.DeniesAllActions(actor))
            return Legality.Refused(LegalityBar.Condition, "a creature that can take no actions cannot take total defense (PHB p.307-311)");
        if (actor.HasCondition(CombatConditionType.Turned))
            return Legality.Refused(LegalityBar.Condition, "a turned creature flees, or cowers when it cannot (PHB p.159)");
        if (actor.HasCondition(CombatConditionType.Panicked) && !actor.CurrentTurnStart.CorneredInPanic)
            return Legality.Refused(LegalityBar.Condition, "a panicked creature flees; only a cornered one takes total defense (PHB p.311)");
        if (actor.IsGrappling())
            return Legality.Refused(LegalityBar.Condition, "a grappling creature can take only the grapple actions (PHB p.156)");
        if (actor.IsFightingDefensively && actor.HasAttackedThisTurn)
            return Legality.Refused(LegalityBar.Action, "total defense cannot be combined with fighting defensively (PHB p.142)");
        if (actor.HasAttackedThisTurn && actor.Stats.CombatExpertiseValue > 0 && actor.Stats.HasFeat("Combat Expertise"))
            return Legality.Refused(LegalityBar.Action, "total defense cannot be combined with Combat Expertise (PHB p.142)");
        if (!ActionCosts.CanAfford(actor, ActionCostType.Standard))
            return Legality.Refused(LegalityBar.Action, "total defense needs a standard action (PHB p.142)");
        return Legality.Allowed();
    }
}

/// <summary>
/// Total defense (status New; PHB p.142, CMB-179): pays the standard action through ActionCosts.TryPay
/// (CharacterController.CommitStandardAction, so a disabled creature's act is strenuous, PHB p.145), then sets
/// CharacterController.IsInTotalDefense and the dodge bonus (BeginTotalDefense) and logs the action. The PC's Total Defense
/// button (GameManager.RunPlayerAction, UiAfter ShowChoices) and the AI (GameManager.RunAIAction: the cornered panicked
/// creature, PHB p.311) run it alike.
/// </summary>
public sealed class TotalDefenseActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor) => TotalDefenseRules.Check(actor);

    public Legality CanExecute(ActionContext ctx)
        => ctx == null ? Legality.Refused(LegalityBar.Other, "no context") : CanStart(ctx.Actor);

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        CharacterController actor = ctx != null ? ctx.Actor : null;
        Legality legal = CanExecute(ctx);
        if (!legal.Ok)
        {
            o.Refuse(legal);
            yield break;
        }
        if (!ActionCosts.TryPay(actor, ActionCostType.Standard))
        {
            o.Refuse(Legality.Refused(LegalityBar.Action, "total defense needs a standard action (PHB p.142)"));
            yield break;
        }

        int bonus = TotalDefenseRules.DodgeBonus(actor.Stats);
        actor.BeginTotalDefense(bonus); // also ends an undeclared fighting-defensively stance (PHB p.142: they do not combine)
        o.Performed = true;

        GameManager gm = ExecutorUtil.Gm;
        if (gm != null)
        {
            gm.CombatUI?.ShowCombatLog(CombatLogHelper.TotalDefense(actor.Stats.CharacterName, bonus));
            gm.Combat_UpdateAllStatsUI();
        }
    }
}
