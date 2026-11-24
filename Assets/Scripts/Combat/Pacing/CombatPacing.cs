using UnityEngine;

/// <summary>Whose pacing a combat wait follows: a player-run turn's (always 1x) or an AI-decided turn's.</summary>
public enum PaceKind
{
    Player,
    Ai
}

/// <summary>
/// The one source of combat pacing waits (grouped AI turns step 1, owner request 2026-10-30;
/// docs/designs/grouped_ai_turns.md "The pacing service and the speed factor"). Every pacing pause in game code is
/// <c>yield return CombatPacing.Wait(seconds)</c> at the spot where it always stood, with the site's own seconds (a
/// constant or the caller's variable: the service multiplies, it never chooses), and every movement step tween reads
/// <see cref="StepSeconds"/>. Pacing is presentation only: no rules outcome, die roll or trace event may depend on it.
///
/// This step builds the Live branch only, with <see cref="Speed"/> fixed at 1, so every call returns exactly the object
/// the site built before (a PURE refactor: identical trace hashes and frames). The AI speed factor (step 6) and the
/// recording of a group (stage 2) come later.
///
/// <see cref="Wait"/> always returns a <see cref="WaitForSeconds"/>, never an IEnumerator, a CustomYieldInstruction or
/// null: GameManager.RunAIActionNow drains any IEnumerator it is handed in place (and InlineAction passes a leaf yield
/// through unchanged), so a wait of another type would change the synchronous AI actions.
///
/// Exempt (not pacing, never routed here; the source scan in Tests.Combat.CombatPacingTests allows exactly these):
/// <see cref="ExemptWaitSites"/>.
/// </summary>
public static class CombatPacing
{
    /// <summary>
    /// The pacing kind, set only by the <see cref="TurnControl"/> marks: Ai from an AI-decided turn's start until control
    /// returns to the player, Player otherwise. Read by nothing that decides a rule.
    /// </summary>
    public static PaceKind Kind { get; internal set; } = PaceKind.Player;

    /// <summary>
    /// Every Play session starts with the Player kind, even without a domain reload (Enter Play Mode Options) or after
    /// Play was left in the middle of an AI turn, so the step 6 speed factor never inherits a stale kind.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Kind = PaceKind.Player;
    }

    /// <summary>The AI speed factor. Fixed at 1 until grouped AI turns step 6 reads GameSettings.aiTurnSpeed.</summary>
    public static float Speed => 1f;

    /// <summary>
    /// The pause of <paramref name="seconds"/> seconds that a combat coroutine yields. Live, speed 1: a new
    /// WaitForSeconds of exactly the caller's seconds, as the site built itself before.
    /// </summary>
    public static WaitForSeconds Wait(float seconds)
    {
        return new WaitForSeconds(seconds);
    }

    /// <summary>
    /// The seconds one movement step tweens for (CharacterController.MoveAlongPath, which still clamps the result to
    /// 0.01 s). Live, speed 1: the caller's value.
    /// </summary>
    public static float StepSeconds(float secondsPerStep)
    {
        return secondsPerStep;
    }

    /// <summary>
    /// The game-code waits that are not pacing and stay <c>new WaitForSeconds</c>: file (under Assets/Scripts), method,
    /// and how many waits that method holds. CharacterController.RunGrappleAlternatingDisplay is an animation that polls
    /// the grapple state (it must not blink faster with the speed factor or every frame while held);
    /// GrappleSystem.CheckMenuAfterDelay is a debug check of the menu flow.
    /// </summary>
    public static readonly (string File, string Method, int Waits)[] ExemptWaitSites =
    {
        ("Character/Controller/CharacterController.cs", "RunGrappleAlternatingDisplay", 2),
        ("Combat/Maneuvers/GrappleSystem.cs", "CheckMenuAfterDelay", 3),
    };
}
