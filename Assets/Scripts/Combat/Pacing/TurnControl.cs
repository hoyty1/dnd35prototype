/// <summary>
/// The presentation-only turn marks of grouped AI turns (docs/designs/grouped_ai_turns.md "The group"; owner request
/// 2026-10-30). They sit on the turn paths PCs and NPCs already share and decide no rule: no mark reads IsControllable,
/// rolls a die or changes the turn chain (CORE-012 re-entrancy included). In stage 1 each mark only sets
/// <see cref="CombatPacing.Kind"/>, which switches the AI speed factor (step 6) on and off; the group itself (open,
/// close, the window cap) comes with stage 2.
/// </summary>
public static class TurnControl
{
    /// <summary>
    /// An AI-decided turn starts: GameManager.OnTurnStarted's NPC branch, right before SingleNPCTurnFromInitiative is
    /// started, and GameManager.BeginForcedPCTurn, right before the forced turn is started. Pacing follows the AI from
    /// here until control returns to the player (C2).
    /// </summary>
    public static void AiTurnStarting(CharacterController actor)
    {
        CombatPacing.Kind = PaceKind.Ai;
    }

    /// <summary>
    /// Control returns to the player: GameManager.ShowActionChoices, right before CombatUI.ShowActionButtonsForCharacter.
    /// </summary>
    public static void PlayerControlReturned(CharacterController pc)
    {
        CombatPacing.Kind = PaceKind.Player;
    }

    /// <summary>
    /// The combat is over (HandleCombatVictoryDetected and HandleCombatDefeatDetected right after the phase is set,
    /// OnCombatEnded) or reset (ResetCombatStateForNextEncounter): pacing is the player's again.
    /// </summary>
    public static void CombatEnded()
    {
        CombatPacing.Kind = PaceKind.Player;
        // A presentation boundary (step 8; trace only in Passthrough). Step 9 closes the group here.
        CombatPresentation.Boundary(PresentationBoundary.CombatEnd);
    }

    /// <summary>
    /// An interactive prompt to a player-run creature opens (the presentation facade, stage 2, and
    /// GameManager.PromptAttackedReaction, step 10). It will close the group; it does not change the pacing kind, because
    /// the speed factor runs from an AI-decided turn's start until control returns (C2). No caller yet.
    /// </summary>
    public static void PromptOpened()
    {
    }
}
