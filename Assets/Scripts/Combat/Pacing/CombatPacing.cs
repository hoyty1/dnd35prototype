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
/// Live branch (steps 1 and 6): with the Player kind every call returns exactly the object the site built before (a
/// new WaitForSeconds of the caller's seconds, the caller's step seconds); with the Ai kind, from an AI-decided turn's
/// mark until control returns to the player (design choice C2), the seconds are divided by the AI speed factor
/// (<see cref="Speed"/>, GameSettings.aiTurnSpeed). PC-turn pacing (AfterAttackDelay, DelayedEndActivePCTurn,
/// ReturnToActionChoicesAfterDelay, PromptFullAttackStep, an NPC's AoO dwell during the PC's walk) runs under the
/// Player kind, so it stays at 1x. The factor is NEUTRAL: every scenario trace hash is identical at any speed, in the
/// harness's fast and fine modes (docs/TESTING.md 3.4). The recording of a group (stage 2) comes later.
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
    /// Play was left in the middle of an AI turn, so the speed factor never inherits a stale kind.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Kind = PaceKind.Player;
    }

    /// <summary>
    /// The AI speed factor (grouped AI turns step 6): GameSettings.aiTurnSpeed (1, 1.5, 2, 3 or 4; default 2x, Q4), read
    /// live, so a change applies to the next wait. It scales a wait or a step only while <see cref="Kind"/> is Ai.
    /// </summary>
    public static float Speed => GameSettings.CurrentAiTurnSpeed;

    /// <summary>
    /// The pure pacing rule: <paramref name="seconds"/> under the Player kind (or at a speed of 1 or less, a guard), and
    /// <paramref name="seconds"/> / <paramref name="speed"/> under the Ai kind.
    /// </summary>
    public static float Scaled(float seconds, PaceKind kind, float speed)
    {
        if (kind != PaceKind.Ai || !(speed > 1f))
            return seconds;
        return seconds / speed;
    }

    /// <summary>
    /// The pause of <paramref name="seconds"/> seconds that a combat coroutine yields: a new WaitForSeconds of the
    /// caller's seconds under the Player kind (as the site built itself before), of seconds / <see cref="Speed"/> under
    /// the Ai kind.
    /// </summary>
    public static WaitForSeconds Wait(float seconds)
    {
        return new WaitForSeconds(Scaled(seconds, Kind, Speed));
    }

    /// <summary>
    /// The seconds one movement step tweens for (CharacterController.MoveAlongPath, which still clamps the result to
    /// 0.01 s): the caller's value under the Player kind, the value / <see cref="Speed"/> under the Ai kind.
    /// </summary>
    public static float StepSeconds(float secondsPerStep)
    {
        return Scaled(secondsPerStep, Kind, Speed);
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
