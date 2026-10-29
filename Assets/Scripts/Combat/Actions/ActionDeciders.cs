using System;
using System.Collections.Generic;

/// <summary>The answer to <see cref="IActionDecider.ConfirmProvocation"/>.</summary>
public enum ProvocationAnswer
{
    /// <summary>Go on with the action and take the attacks of opportunity.</summary>
    Proceed,

    /// <summary>The player backed out: the action is not taken.</summary>
    Cancel,

    /// <summary>No prompt could be shown (one is already open, or the action has its own prompt): the action is not taken.</summary>
    Unavailable,
}

/// <summary>
/// The actor's choices inside a catalog action (batch 13 step 3; the IAttackDecider and ICastDecider pattern): the rules
/// code asks, the UI decider prompts the player, the AI decider answers at once. Each method answers through
/// <c>answer</c> exactly once.
/// </summary>
public interface IActionDecider
{
    /// <summary>True when every choice is answered at once (the AI).</summary>
    bool IsSynchronous { get; }

    /// <summary>
    /// Whether to go on with an action that provokes from <paramref name="threateners"/> (PHB p.137). The UI shows the
    /// attack-of-opportunity prompt (GameManager.ShowAoOActionConfirmation); the AI always goes on.
    /// </summary>
    void ConfirmProvocation(GameManager gm, CharacterController actor, AoOProvokingAction action, string actionName,
        List<CharacterController> threateners, Action<ProvocationAnswer> answer);
}

/// <summary>The player's answers: the attack-of-opportunity prompt.</summary>
public sealed class UiActionDecider : IActionDecider
{
    public static readonly UiActionDecider Instance = new UiActionDecider();

    public bool IsSynchronous => false;

    public void ConfirmProvocation(GameManager gm, CharacterController actor, AoOProvokingAction action, string actionName,
        List<CharacterController> threateners, Action<ProvocationAnswer> answer)
    {
        if (gm == null)
        {
            answer?.Invoke(ProvocationAnswer.Proceed);
            return;
        }

        // A cast asks through the cast core's own prompt (cast defensively or not, GameManager.CastDeciders.cs); this
        // prompt has no Cast Defensively answer, so a catalog CastSpell provocation is not shown here.
        if (action == AoOProvokingAction.CastSpell)
        {
            answer?.Invoke(ProvocationAnswer.Unavailable);
            return;
        }

        string label = string.IsNullOrEmpty(actionName) ? action.ToString() : actionName;
        var info = new AoOProvokingActionInfo
        {
            ActionType = action,
            ActionName = label.ToUpperInvariant(),
            ActionDescription = label + " (provokes attacks of opportunity)",
            Actor = actor,
            ThreateningEnemies = threateners != null ? new List<CharacterController>(threateners) : new List<CharacterController>(),
            OnProceed = () => answer?.Invoke(ProvocationAnswer.Proceed),
            OnCancel = () => answer?.Invoke(ProvocationAnswer.Cancel),
            OnCastDefensively = () => answer?.Invoke(ProvocationAnswer.Cancel),
        };
        // A prompt already open cannot take a second one: the action is not taken (not a player cancel).
        if (!gm.TryShowActionAoOConfirmation(info))
            answer?.Invoke(ProvocationAnswer.Unavailable);
    }
}

/// <summary>The AI's answers: it always goes on with the action it chose.</summary>
public sealed class AiActionDecider : IActionDecider
{
    public static readonly AiActionDecider Instance = new AiActionDecider();

    public bool IsSynchronous => true;

    public void ConfirmProvocation(GameManager gm, CharacterController actor, AoOProvokingAction action, string actionName,
        List<CharacterController> threateners, Action<ProvocationAnswer> answer)
        => answer?.Invoke(ProvocationAnswer.Proceed);
}

/// <summary>The decider of a creature's catalog-action choices: the catalog's one IsControllable read.</summary>
public static class ActionDeciders
{
    /// <summary>
    /// The UI decider for a controllable creature, else the AI's. <paramref name="overrideSource"/> fixes the source: the
    /// AI runner, the harness and a forced PC turn pass <see cref="ActorSource.AI"/> (C7), the PC runner
    /// <see cref="ActorSource.Ui"/>.
    /// </summary>
    public static IActionDecider For(CharacterController actor, ActorSource? overrideSource = null)
    {
        ActorSource source = overrideSource ?? (actor != null && actor.IsControllable ? ActorSource.Ui : ActorSource.AI);
        return source == ActorSource.Ui ? (IActionDecider)UiActionDecider.Instance : AiActionDecider.Instance;
    }
}
