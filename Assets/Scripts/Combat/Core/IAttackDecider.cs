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
    /// The Cleave target among <paramref name="candidates"/> (PHB p.92, D7). Not called yet: step 15 wires Cleave through
    /// it, with the UI prompt for more than one candidate and the AI's target scoring. Today both answer the first.
    /// </summary>
    Coroutine ChooseCleaveTarget(GameManager gm, CharacterController attacker, IList<CharacterController> candidates,
        Action<CharacterController> answer);

    /// <summary>
    /// Whether <paramref name="reactor"/> uses <paramref name="reaction"/> against the creature that attacked it (the
    /// bombardier beetle's acid spray as a reaction, D1). Not called yet: step 15 wires it. Today both decline.
    /// </summary>
    Coroutine ChooseAttackedReaction(GameManager gm, CharacterController reactor, CharacterController attacker, string reaction,
        Action<bool> answer);
}

/// <summary>The player's choices, through the existing prompts (GameManager.PromptImprovedGrabChoice, PromptHasteNaturalAttackChoice).</summary>
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
        answer?.Invoke(candidates != null && candidates.Count > 0 ? candidates[0] : null);
        return null;
    }

    public Coroutine ChooseAttackedReaction(GameManager gm, CharacterController reactor, CharacterController attacker, string reaction,
        Action<bool> answer)
    {
        answer?.Invoke(false);
        return null;
    }
}

/// <summary>The AI's choices, answered at once from the creature's AIProfile (GameManager.ChooseImprovedGrabForAI, ChooseHasteNaturalAttackIndexForAI).</summary>
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
        answer?.Invoke(candidates != null && candidates.Count > 0 ? candidates[0] : null);
        return null;
    }

    public Coroutine ChooseAttackedReaction(GameManager gm, CharacterController reactor, CharacterController attacker, string reaction,
        Action<bool> answer)
    {
        answer?.Invoke(false);
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
