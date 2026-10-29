using UnityEngine;

/// <summary>
/// The PC Smite button and the AI's smite declaration: thin wrappers over the one smite rule, SmiteRules (CHR-020,
/// CMB-021, AI-056). Both sides declare the smite (no action) and then attack through their ordinary attack paths, which
/// apply and use up the declared smite on the next melee attack (CharacterController.PerformSingleAttackWithCritCore).
/// Covers the paladin's Smite Evil (PHB p.44) and the celestial and fiendish templates' smites (MM p.31, p.107).
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// Whether the Smite button does something now: declare a smite (an unused smite and an attack still to make this
    /// turn), or withdraw one declared this turn. <paramref name="reason"/> says why not.
    /// </summary>
    public bool CanUseSmiteButton(CharacterController actor, out string reason)
    {
        reason = string.Empty;
        if (actor == null || actor.Stats == null || actor.Actions == null)
        {
            reason = "No active character";
            return false;
        }

        if (actor.Stats.DeclaredSmite != SmiteSource.None)
            return true;

        if (actor.HasCondition(CombatConditionType.Turned))
        {
            reason = "Turned condition";
            return false;
        }

        if (!SmiteRules.CanDeclare(actor, out _, out reason))
            return false;

        bool attackLeft = actor.Actions.HasStandardAction || actor.Actions.HasFullRoundAction
            || actor.CanCommitAttack(AttackStepKind.MainHand, out _);
        if (!attackLeft)
        {
            reason = "No attack left this turn";
            return false;
        }

        return true;
    }

    /// <summary>
    /// The PC Smite button: declares the smite for the PC's next melee attack this turn (the Attack, Full Attack or
    /// Charge button then makes the attack), or withdraws a declaration no attack has used yet (no use spent).
    /// </summary>
    public void OnSmiteButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null)
            return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "smite"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "smite"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (SmiteRules.CancelDeclaration(pc.Stats))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("✦", $"{pc.Stats.CharacterName} withdraws the declared smite (no use spent)."));
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!CanUseSmiteButton(pc, out string reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} cannot smite: {reason}."));
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!DeclareSmite(pc, out reason))
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} cannot smite: {reason}."));
        CombatUI?.UpdateActionButtons(pc);
    }

    /// <summary>
    /// The AI declares its smite before its melee sequence or charge against <paramref name="target"/> when its profile
    /// chooses it (AIProfile.ShouldDeclareSmite: only against a creature it believes is of the smitten alignment) and
    /// the shared rules allow it (SmiteRules.CanDeclare, the core the PC Smite button calls). Not against a creature it
    /// holds with one body part, which it may not attack (MM p.310; CMB-163).
    /// </summary>
    private bool TryNPCDeclareSmite(CharacterController npc, CharacterController target)
    {
        if (npc == null || npc.Stats == null || target == null || target.Stats == null)
            return false;
        if (!SmiteRules.CanDeclare(npc, out _, out _))
            return false;
        if (npc.IsBarredFromOrdinaryAttackOn(target, out _))
            return false;

        bool choose = npc.aiProfile != null
            ? npc.aiProfile.ShouldDeclareSmite(npc, target)
            : DND35.AI.AIProfile.DefaultShouldDeclareSmite(npc, target);
        if (!choose)
            return false;

        return DeclareSmite(npc, out _);
    }

    /// <summary>
    /// Declares <paramref name="actor"/>'s smite (SmiteRules.TryDeclare) and logs it: the one declaration of the PC Smite
    /// button, the AI's and the catalog's smite entry. False, with <paramref name="reason"/> and nothing logged, when the
    /// rules refuse it; each caller logs that refusal its own way.
    /// </summary>
    internal bool DeclareSmite(CharacterController actor, out string reason)
    {
        string line = SmiteRules.TryDeclare(actor, out reason);
        if (line == null)
            return false;
        CombatUI?.ShowCombatLog(CombatLogHelper.Color($"✦ {line}", "FFD280"));
        return true;
    }

    /// <summary>The AI's smite declaration for a scenario or caller outside the NPC attack sequence.</summary>
    public bool TryNPCDeclareSmiteForAI(CharacterController npc, CharacterController target) => TryNPCDeclareSmite(npc, target);
}
