using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Who makes a creature's choices: the player through the UI, or the AI (batch 13 adds a forced source).</summary>
public enum ActorSource
{
    Ui,
    AI
}

/// <summary>
/// The attacker's (or a defender's) choices during an attack, PC and NPC alike (batch 12 step 13, the ICastDecider
/// pattern): the rules code asks, the UI decider prompts the player, the AI decider answers from the creature's AIProfile.
/// Each method answers through <c>answer</c>, exactly once, and returns the running prompt to wait on (a coroutine the
/// caller yields), or null when it answered at once. Only <see cref="AttackDeciders.For"/> reads IsControllable.
/// </summary>
public interface IAttackDecider
{
    /// <summary>True when every choice is answered at once (the AI).</summary>
    bool IsSynchronous { get; }

    /// <summary>After an Improved Grab trigger hit: grapple normally, hold with the body part (-20), or skip (MM p.310, CMB-148).</summary>
    Coroutine ChooseImprovedGrab(GameManager gm, CharacterController attacker, CharacterController target, CombatResult triggerHit,
        Action<GameManager.ImprovedGrabChoice> answer);

    /// <summary>
    /// Which natural attack Haste's extra attack uses (PHB p.239; owner decision 2026-10-07, CMB-106): a natural-sequence
    /// index. <paramref name="profile"/> is the AI profile that runs the turn (null: the creature's own);
    /// <paramref name="labelBonusOffset"/> is added to the prompt's labels (the pounce's charge +2).
    /// </summary>
    Coroutine ChooseHasteNaturalAttack(GameManager gm, CharacterController attacker, CharacterController target,
        DND35.AI.AIProfile profile, int labelBonusOffset, Action<int> answer);

    /// <summary>
    /// The Cleave target among <paramref name="candidates"/> (PHB p.92, D7: every creature in reach the attacker
    /// perceives), or null to make no Cleave attack. The UI attacks a lone candidate at once only when it is a foe still in
    /// the fight (a design choice to save a click, recorded with D7 in RULES_COVERAGE 4.1) and prompts otherwise, with "No
    /// Cleave attack"; the AI picks among the foes by its target scoring (GameManager.ChooseCleaveTargetForAI).
    /// </summary>
    Coroutine ChooseCleaveTarget(GameManager gm, CharacterController attacker, IList<CharacterController> candidates,
        Action<CharacterController> answer);

    /// <summary>
    /// Whether <paramref name="reactor"/> uses <paramref name="reaction"/> against the creature that attacked or disturbed
    /// it (the bombardier beetle's acid spray as a reaction, MM p.285, D1). The UI prompts; the AI uses it when the spray's
    /// cone would catch more foes than allies (GameManager.ShouldUseAttackedReactionForAI).
    /// </summary>
    Coroutine ChooseAttackedReaction(GameManager gm, CharacterController reactor, CharacterController attacker, string reaction,
        Action<bool> answer);
}

/// <summary>The player's choices, through the prompts (GameManager.PromptImprovedGrabChoice, PromptHasteNaturalAttackChoice, PromptCleaveTarget, PromptAttackedReaction).</summary>
public sealed class UiAttackDecider : IAttackDecider
{
    public static readonly UiAttackDecider Instance = new UiAttackDecider();

    private UiAttackDecider() { }

    public bool IsSynchronous => false;

    public Coroutine ChooseImprovedGrab(GameManager gm, CharacterController attacker, CharacterController target, CombatResult triggerHit,
        Action<GameManager.ImprovedGrabChoice> answer)
        => gm.StartCoroutine(gm.PromptImprovedGrabChoice(attacker, target, triggerHit != null ? triggerHit.WeaponName : null, answer));

    public Coroutine ChooseHasteNaturalAttack(GameManager gm, CharacterController attacker, CharacterController target,
        DND35.AI.AIProfile profile, int labelBonusOffset, Action<int> answer)
        => gm.StartCoroutine(gm.PromptHasteNaturalAttackChoice(attacker, target, answer, labelBonusOffset));

    public Coroutine ChooseCleaveTarget(GameManager gm, CharacterController attacker, IList<CharacterController> candidates,
        Action<CharacterController> answer)
    {
        if (candidates == null || candidates.Count == 0)
        {
            answer?.Invoke(null);
            return null;
        }
        // A lone foe still in the fight is attacked at once (D7's design choice); a lone ally, a lone downed creature or
        // several candidates are asked, with the option to decline.
        if (candidates.Count == 1 && TeamUtility.IsEnemy(attacker, candidates[0]) && !CombatEndRules.IsOutOfFight(candidates[0]))
        {
            answer?.Invoke(candidates[0]);
            return null;
        }
        if (gm == null)
        {
            answer?.Invoke(null);
            return null;
        }
        return gm.StartCoroutine(gm.PromptCleaveTarget(attacker, candidates, answer));
    }

    public Coroutine ChooseAttackedReaction(GameManager gm, CharacterController reactor, CharacterController attacker, string reaction,
        Action<bool> answer)
    {
        if (gm == null)
        {
            answer?.Invoke(false);
            return null;
        }
        return gm.StartCoroutine(gm.PromptAttackedReaction(reactor, attacker, reaction, answer));
    }
}

/// <summary>The AI's choices, answered at once from the creature's AIProfile (GameManager.ChooseImprovedGrabForAI, ChooseHasteNaturalAttackIndexForAI, ChooseCleaveTargetForAI, ShouldUseAttackedReactionForAI).</summary>
public sealed class AiAttackDecider : IAttackDecider
{
    public static readonly AiAttackDecider Instance = new AiAttackDecider();

    private AiAttackDecider() { }

    public bool IsSynchronous => true;

    public Coroutine ChooseImprovedGrab(GameManager gm, CharacterController attacker, CharacterController target, CombatResult triggerHit,
        Action<GameManager.ImprovedGrabChoice> answer)
    {
        answer?.Invoke(gm.ChooseImprovedGrabForAI(attacker, target));
        return null;
    }

    public Coroutine ChooseHasteNaturalAttack(GameManager gm, CharacterController attacker, CharacterController target,
        DND35.AI.AIProfile profile, int labelBonusOffset, Action<int> answer)
    {
        answer?.Invoke(gm.ChooseHasteNaturalAttackIndexForAI(attacker, target, profile));
        return null;
    }

    public Coroutine ChooseCleaveTarget(GameManager gm, CharacterController attacker, IList<CharacterController> candidates,
        Action<CharacterController> answer)
    {
        CharacterController pick = gm != null
            ? gm.ChooseCleaveTargetForAI(attacker, candidates)
            : (candidates != null && candidates.Count > 0 ? candidates[0] : null);
        answer?.Invoke(pick);
        return null;
    }

    public Coroutine ChooseAttackedReaction(GameManager gm, CharacterController reactor, CharacterController attacker, string reaction,
        Action<bool> answer)
    {
        answer?.Invoke(gm != null && gm.ShouldUseAttackedReactionForAI(reactor, attacker, reaction));
        return null;
    }
}

/// <summary>The decider of a creature's attack choices (the one IsControllable read of the attack paths).</summary>
public static class AttackDeciders
{
    /// <summary>
    /// The UI decider for a controllable creature, else the AI's. <paramref name="overrideSource"/> fixes the source: an
    /// NPC turn path passes <see cref="ActorSource.AI"/> (it is AI-run for every creature on it), and batch 13 passes a
    /// forced source.
    /// </summary>
    public static IAttackDecider For(CharacterController actor, ActorSource? overrideSource = null)
    {
        ActorSource source = overrideSource ?? (actor != null && actor.IsControllable ? ActorSource.Ui : ActorSource.AI);
        return source == ActorSource.Ui ? (IAttackDecider)UiAttackDecider.Instance : AiAttackDecider.Instance;
    }
}
