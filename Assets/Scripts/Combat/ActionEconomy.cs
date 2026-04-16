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

    // D&D 3.5 disabled state support: one move OR one standard action each turn.
    public bool SingleActionOnly;

    // ========== AVAILABILITY CHECKS ==========

    /// <summary>Can the character still take a Move Action this turn?</summary>
    public bool HasMoveAction
    {
        get
        {
            if (FullRoundActionUsed || MoveActionUsed)
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
            if (FullRoundActionUsed || StandardActionUsed || StandardConvertedToMove)
                return false;
            if (SingleActionOnly && MoveActionUsed)
                return false;
            return true;
        }
    }

    /// <summary>Can the character take a Full-Round Action? (requires neither standard nor move used yet)</summary>
    public bool HasFullRoundAction => !SingleActionOnly && !FullRoundActionUsed && !StandardActionUsed && !MoveActionUsed && !StandardConvertedToMove;

    /// <summary>Can the character convert their standard action into a second move?</summary>
    public bool CanConvertStandardToMove => !SingleActionOnly && HasStandardAction && MoveActionUsed;

    /// <summary>Can the character take a Swift Action?</summary>
    public bool HasSwiftAction => !SwiftActionUsed;

    /// <summary>Are all meaningful actions (standard+move or full-round) spent?</summary>
    public bool AllMainActionsSpent
    {
        get
        {
            if (FullRoundActionUsed) return true;
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

    public void UseMoveAction()
    {
        MoveActionUsed = true;
        if (SingleActionOnly)
            StandardConvertedToMove = true;
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
    }

    // ========== STATUS STRING ==========

    public string GetStatusString()
    {
        if (FullRoundActionUsed) return "Full-Round Action used - Turn complete";

        string status = SingleActionOnly ? "[Disabled: one action only] " : "";
        if (HasMoveAction) status += "[Move] ";
        else status += "[Move USED] ";

        if (HasStandardAction) status += "[Standard] ";
        else if (StandardConvertedToMove) status += "[Std→Move] ";
        else status += "[Standard USED] ";

        if (HasSwiftAction) status += "[Swift] ";

        return status.Trim();
    }
}
