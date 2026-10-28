/// <summary>
/// D&D 3.5 Action Economy tracker for a single turn.
/// Each turn a character gets: 1 Standard + 1 Move (or 1 Full-Round), plus 1 Swift and unlimited Free actions.
/// A Standard Action can be downgraded to a Move Action (giving 2 moves total).
/// A Full-Round Action consumes both Standard and Move actions.
/// </summary>
[System.Serializable]
public class ActionEconomy
{
    // Track which actions have been spent
    public bool MoveActionUsed;
    public bool StandardActionUsed;
    public bool FullRoundActionUsed;
    public bool SwiftActionUsed;

    // Whether the standard action was converted to a second move action
    public bool StandardConvertedToMove;

    // Whether a 5-foot step has been taken this turn
    public bool HasMoved5Ft;

    // One move OR one standard action each turn, and no full-round action: disabled (PHB p.145), staggered (PHB p.146,
    // glossary p.313), slowed (PHB p.280), or a creature limited to single actions (zombies, MM p.266). Written by
    // CharacterController.RefreshActionLimits; a plain field so tests can set it directly.
    public bool SingleActionOnly;

    // Only a single move action each turn: nauseated (PHB p.310 glossary; DMG p.301). No standard or full-round
    // action and no conversion of the standard action. Written by CharacterController.RefreshActionLimits.
    public bool MoveActionOnly;

    // A disabled creature finished a strenuous act this turn that will leave it dying once its 1 damage is dealt
    // (PHB p.145: after the act it is "now" at -1 and dying), so it takes no further action, 5-ft step or swift or
    // quickened casting this turn. Set by CharacterController.MarkStrenuousAction (not for Diehard or temporary hit
    // points, which keep it disabled); cleared by Reset and by a refunded cast's snapshot.
    public bool NoFurtherActions;

    // ========== AVAILABILITY CHECKS ==========

    /// <summary>Can the character still take a Move Action this turn?</summary>
    public bool HasMoveAction
    {
        get
        {
            if (NoFurtherActions || FullRoundActionUsed || MoveActionUsed)
                return false;
            if (SingleActionOnly && (StandardActionUsed || StandardConvertedToMove))
                return false;
            return true;
        }
    }

    /// <summary>Can the character still take a Standard Action?</summary>
    public bool HasStandardAction
    {
        get
        {
            if (MoveActionOnly || NoFurtherActions)
                return false;
            if (FullRoundActionUsed || StandardActionUsed || StandardConvertedToMove)
                return false;
            if (SingleActionOnly && MoveActionUsed)
                return false;
            return true;
        }
    }

    /// <summary>Can the character take a Full-Round Action? (requires neither standard nor move used yet)</summary>
    public bool HasFullRoundAction => !SingleActionOnly && !MoveActionOnly && !NoFurtherActions && !FullRoundActionUsed && !StandardActionUsed && !MoveActionUsed && !StandardConvertedToMove;

    /// <summary>Can the character convert their standard action into a second move?</summary>
    public bool CanConvertStandardToMove => !SingleActionOnly && !MoveActionOnly && HasStandardAction && MoveActionUsed;

    /// <summary>Can the character take a Swift Action?</summary>
    public bool HasSwiftAction => !SwiftActionUsed && !NoFurtherActions;

    /// <summary>Are all meaningful actions (standard+move or full-round) spent?</summary>
    public bool AllMainActionsSpent
    {
        get
        {
            if (FullRoundActionUsed || NoFurtherActions) return true;
            if (MoveActionOnly && MoveActionUsed) return true; // the one move action was the whole turn
            bool moveSpent = MoveActionUsed || StandardConvertedToMove;
            bool stdSpent = StandardActionUsed || StandardConvertedToMove;
            // If both are spent, or standard used and move used
            return (moveSpent || MoveActionUsed) && (stdSpent || StandardActionUsed);
        }
    }

    /// <summary>Check if there are any actions the player can still take.</summary>
    public bool HasAnyActionLeft
    {
        get
        {
            if (FullRoundActionUsed) return false;
            return HasMoveAction || HasStandardAction || CanConvertStandardToMove;
        }
    }

    // ========== ACTION USAGE ==========

    /// <summary>
    /// Spend the move action. Returns false, and changes nothing, when no move action is left
    /// (already used, full-round action taken, or the single action of a disabled turn spent).
    /// A second move must be an explicit <see cref="ConvertStandardToMove"/> (PHB ch.8 double move).
    /// Existing callers may ignore the result.
    /// </summary>
    public bool UseMoveAction()
    {
        if (!HasMoveAction)
            return false;

        MoveActionUsed = true;
        // The one move action of a single-action or move-only turn is the whole turn: lifting the limit later in the
        // turn gives no standard action back (CharacterController.RefreshActionLimits).
        if (SingleActionOnly || MoveActionOnly)
            StandardConvertedToMove = true;
        return true;
    }

    public void UseStandardAction()
    {
        StandardActionUsed = true;
        if (SingleActionOnly)
            MoveActionUsed = true;
    }

    public void UseFullRoundAction()
    {
        FullRoundActionUsed = true;
    }

    public void UseSwiftAction()
    {
        SwiftActionUsed = true;
    }

    /// <summary>Convert the remaining standard action into a move action (for double move).</summary>
    public void ConvertStandardToMove()
    {
        StandardConvertedToMove = true;
        if (SingleActionOnly)
            MoveActionUsed = true;
    }

    // ========== RESET ==========

    public void Reset()
    {
        MoveActionUsed = false;
        StandardActionUsed = false;
        FullRoundActionUsed = false;
        SwiftActionUsed = false;
        StandardConvertedToMove = false;
        SingleActionOnly = false;
        MoveActionOnly = false;
        NoFurtherActions = false;
        HasMoved5Ft = false;
    }

    // ========== STATUS STRING ==========

    public string GetStatusString()
    {
        if (FullRoundActionUsed) return "Full-Round Action used - Turn complete";

        string status = MoveActionOnly ? "[Move action only] " : SingleActionOnly ? "[Single action only] " : "";
        if (HasMoveAction) status += "[Move] ";
        else status += "[Move USED] ";

        if (HasStandardAction) status += "[Standard] ";
        else if (StandardConvertedToMove) status += "[Std→Move] ";
        else status += "[Standard USED] ";

        if (HasSwiftAction) status += "[Swift] ";

        return status.Trim();
    }
}
