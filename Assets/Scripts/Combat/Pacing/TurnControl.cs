/// <summary>
/// The presentation-only turn marks of grouped AI turns (docs/designs/grouped_ai_turns.md "The group"; owner request
/// 2026-10-30). They sit on the turn paths PCs and NPCs already share and decide no rule: no mark reads IsControllable,
/// rolls a die or changes the turn chain (CORE-012 re-entrancy included). Each mark sets <see cref="CombatPacing.Kind"/>,
/// which switches the AI speed factor (step 6) on and off, and since step 9 opens or closes the group the presentation
/// records (CombatPresentation.OpenGroup and CloseGroup): a group opens at an AI-decided turn's mark when
/// GameSettings.groupAiTurns is on (read once, when the group opens) and the combat is not over, and closes at the first
/// of the control beat, a prompt to a player-run creature, combat end and a reset. The window cap (S5) and the stop
/// marks (S2, S3) come with steps 10 and 11.
/// </summary>
public static class TurnControl
{
    /// <summary>
    /// An AI-decided turn starts: GameManager.OnTurnStarted's NPC branch, right before SingleNPCTurnFromInitiative is
    /// started, and GameManager.BeginForcedPCTurn, right before the forced turn is started. Pacing follows the AI from
    /// here until control returns to the player (C2). With grouping on and no group open, a group opens here (the
    /// character sheet, skills and inventory windows close, C6); the turn is the group's next lane.
    /// </summary>
    public static void AiTurnStarting(CharacterController actor)
    {
        CombatPacing.Kind = PaceKind.Ai;
        if (!CombatPresentation.Holding && GameSettings.CurrentGroupAiTurns && !CombatIsOver())
        {
            if (CombatPresentation.OpenGroup())
            {
                GameManager gm = GameManager.Instance;
                if (gm != null)
                    gm.ClosePlayerWindowsForGroup();
            }
        }
        if (CombatPresentation.Holding)
        {
            GameManager gm = GameManager.Instance;
            CombatPresentation.NoteLane(actor, gm != null ? gm.CurrentRound : 0);
        }
    }

    /// <summary>
    /// Control returns to the player: GameManager.ShowActionChoices, right before CombatUI.ShowActionButtonsForCharacter.
    /// An open group closes here, and the button show that follows is its control beat (S1).
    /// </summary>
    public static void PlayerControlReturned(CharacterController pc)
    {
        CombatPacing.Kind = PaceKind.Player;
        CombatPresentation.CloseGroup(GroupStop.Control);
    }

    /// <summary>
    /// The combat is over (HandleCombatVictoryDetected and HandleCombatDefeatDetected right after the phase is set,
    /// OnCombatEnded) or reset (ResetCombatStateForNextEncounter): pacing is the player's again, and an open group closes
    /// (S4: its beats still play, and the loot window and the defeat panel wait for them).
    /// </summary>
    public static void CombatEnded()
    {
        CombatPacing.Kind = PaceKind.Player;
        // A presentation boundary (step 8).
        CombatPresentation.Boundary(PresentationBoundary.CombatEnd);
        CombatPresentation.CloseGroup(GroupStop.CombatEnd);
    }

    /// <summary>
    /// An interactive prompt to a player-run creature opens (CombatUI.ShowConfirmationDialog while the presentation holds
    /// or is behind, step 9; GameManager.PromptAttackedReaction in step 10): the group closes. It does not change the
    /// pacing kind, because the speed factor runs from an AI-decided turn's start until control returns (C2).
    /// </summary>
    public static void PromptOpened()
    {
        CombatPresentation.CloseGroup(GroupStop.Prompt);
    }

    private static bool CombatIsOver()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentPhase == GameManager.TurnPhase.CombatOver;
    }
}
