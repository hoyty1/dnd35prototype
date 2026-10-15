using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The PC Wild Shape button and the AI's wild shape: thin wrappers over the one rule, WildShapeRules (PHB p.37;
/// CHR-020). The PC presses the button and picks a form from the list (or presses it again in animal form to change
/// back); the AI picks a form with AIProfile.ChooseWildShapeForm. Both log the same line and refresh the stats UI.
/// </summary>
public partial class GameManager
{
    /// <summary>Whether the Wild Shape button is usable now: a form can be taken, or the druid can change back.</summary>
    public bool CanUseWildShapeButton(CharacterController actor, out string reason)
    {
        if (actor != null && actor.Stats != null && WildShapeRules.IsWildShaped(actor.Stats))
            return WildShapeRules.CanRevert(actor, out reason);
        if (!WildShapeRules.CanAssume(actor, out reason))
            return false;
        if (actor.HasCondition(CombatConditionType.Turned))
        {
            reason = "Turned condition";
            return false;
        }
        if (WildShapeRules.GetAvailableForms(actor.Stats).Count == 0)
        {
            reason = "No animal form available";
            return false;
        }
        return true;
    }

    /// <summary>
    /// The PC Wild Shape button: in animal form it changes back (a standard action); otherwise it opens the list of the
    /// forms the druid may take, and the choice goes to <see cref="OnWildShapeFormSelected"/>.
    /// </summary>
    public void OnWildShapeButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null)
            return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "wild shape"))
            return;

        if (!CanUseWildShapeButton(pc, out string reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} cannot use Wild Shape: {reason}."));
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (WildShapeRules.IsWildShaped(pc.Stats))
        {
            EndAttackSequence();
            ContinuePCTurnAfterWildShape(pc, ExecuteWildShapeRevert(pc));
            return;
        }

        List<NPCDefinition> forms = WildShapeRules.GetAvailableForms(pc.Stats);
        var labels = new List<string>(forms.Count);
        for (int i = 0; i < forms.Count; i++)
            labels.Add(DescribeWildShapeForm(forms[i]));

        if (CombatUI == null)
            return;
        CombatUI.ShowPickUpItemSelection(
            actorName: pc.Stats.CharacterName,
            itemOptions: labels,
            onSelect: index =>
            {
                if (index < 0 || index >= forms.Count)
                {
                    ShowActionChoices();
                    return;
                }
                OnWildShapeFormSelected(forms[index].Id);
            },
            onCancel: () =>
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", "Wild Shape cancelled."));
                ShowActionChoices();
            },
            titleOverride: $"WILD SHAPE ({WildShapeRules.UsesLeft(pc.Stats)}/{WildShapeRules.UsesPerDay(pc.Stats)} left today)",
            bodyOverride: $"{pc.Stats.CharacterName}: choose an animal (standard action; HD up to druid level {WildShapeRules.DruidLevel(pc.Stats)}):",
            optionButtonColorOverride: new Color(0.24f, 0.42f, 0.2f, 1f));
    }

    /// <summary>The PC's form choice (the Wild Shape list's buttons, and the scenario driver): takes the form.</summary>
    public void OnWildShapeFormSelected(string formId)
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null)
            return;

        EndAttackSequence();
        ContinuePCTurnAfterWildShape(pc, ExecuteWildShape(pc, formId));
    }

    /// <summary>
    /// After the PC's change of form: the action menu again while she can act; the turn ends when the change of hit
    /// points left her dying, unconscious or dead (PHB p.145); nothing when the fight is over.
    /// </summary>
    private void ContinuePCTurnAfterWildShape(CharacterController pc, bool performed)
    {
        if (CurrentPhase == TurnPhase.CombatOver)
            return;
        if (!performed)
        {
            ShowActionChoices();
            return;
        }
        if (pc == null || !pc.CanTakeTurnActions())
        {
            EndActivePCTurn();
            return;
        }
        StartCoroutine(AfterAttackDelay(pc, 0.6f));
    }

    /// <summary>One line for the form list: name, Hit Dice, size and natural attacks.</summary>
    public static string DescribeWildShapeForm(NPCDefinition form)
    {
        if (form == null)
            return "?";
        var attacks = new List<string>();
        if (form.NaturalAttacks != null)
        {
            foreach (NaturalAttackDefinition a in form.NaturalAttacks)
            {
                if (a == null)
                    continue;
                attacks.Add((a.Count > 1 ? a.Count + " " : string.Empty) + a.Name + " " + a.DamageCount + "d" + a.DamageDice);
            }
        }
        string extras = form.HasTripAttack ? ", trip" : string.Empty;
        if (form.HasImprovedGrab) extras += ", improved grab";
        if (form.HasPounce) extras += ", pounce";
        return $"{form.Name} ({form.ResolveTotalHitDice()} HD, {form.SizeCategory}; Str {form.STR} Dex {form.DEX} Con {form.CON}; NA +{form.NaturalArmorBonus}; {string.Join(", ", attacks)}{extras})";
    }

    /// <summary>
    /// The shared executor behind the PC choice and the AI: WildShapeRules.Assume, the combat log and the stats UI.
    /// Returns true when the druid took the form (the standard action and a use are spent).
    /// </summary>
    private bool ExecuteWildShape(CharacterController actor, string formId)
    {
        WildShapeResult result = WildShapeRules.Assume(actor, formId);
        string actorName = actor != null && actor.Stats != null ? actor.Stats.CharacterName : "?";
        if (!result.Performed)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{actorName} cannot use Wild Shape: {result.Refusal}."));
            return false;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Buff("🐾", result.Log));
        UpdateAllStatsUI();
        // A form with less Constitution, or gear whose Constitution melded, can drop her (PHB p.145).
        EvaluateCombatEnd("WildShape.Assume");
        return true;
    }

    /// <summary>The shared executor for changing back (PC button and AI): WildShapeRules.Revert, the log and the UI.</summary>
    private bool ExecuteWildShapeRevert(CharacterController actor)
    {
        WildShapeResult result = WildShapeRules.Revert(actor);
        string actorName = actor != null && actor.Stats != null ? actor.Stats.CharacterName : "?";
        if (!result.Performed)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{actorName} cannot change back: {result.Refusal}."));
            return false;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐾", result.Log));
        UpdateAllStatsUI();
        EvaluateCombatEnd("WildShape.Revert");
        return true;
    }

    /// <summary>The AI (and scenario) entry: takes <paramref name="formId"/> through the shared executor.</summary>
    public bool TryWildShapeForAI(CharacterController npc, string formId) => ExecuteWildShape(npc, formId);

    /// <summary>The AI (and scenario) entry for changing back through the shared executor.</summary>
    public bool TryWildShapeRevertForAI(CharacterController npc) => ExecuteWildShapeRevert(npc);

    /// <summary>
    /// The AI's wild shape at the start of its action: when its profile picks a form (AIProfile.ChooseWildShapeForm)
    /// the druid takes it with its standard action. Returns true when it did.
    /// </summary>
    public bool TryNPCWildShapeTurnForAI(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || !WildShapeRules.HasAbility(npc.Stats))
            return false;
        if (!WildShapeRules.CanAssume(npc, out _))
            return false;

        List<CharacterController> all = GetAllCharacters();
        NPCDefinition form = npc.aiProfile != null
            ? npc.aiProfile.ChooseWildShapeForm(npc, all)
            : DND35.AI.AIProfile.DefaultWildShapeForm(npc, all);
        if (form == null)
            return false;
        return ExecuteWildShape(npc, form.Id);
    }
}
