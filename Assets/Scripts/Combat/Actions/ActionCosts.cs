/// <summary>
/// The action economy as the catalog sees it (batch 13 step 3): can an actor afford an action type, pay it, and which
/// action a run spent. One rule for both sides over <see cref="ActionEconomy"/> and
/// <see cref="CharacterController.CommitStandardAction"/> (a disabled creature's standard action is strenuous, PHB p.145).
/// ManeuverCost and ManeuverActionCost.For stay the maneuvers' table; ActionCatalogTests checks that they agree.
/// </summary>
public static class ActionCosts
{
    /// <summary>
    /// Whether <paramref name="actor"/> can still pay <paramref name="cost"/> this turn: a move action (or the standard
    /// action converted to one, PHB p.141), the standard action, the full-round action, or the swift action (an
    /// immediate action spends the swift action, as ActionCostType says). A free action and no action always can.
    /// </summary>
    public static bool CanAfford(CharacterController actor, ActionCostType cost)
        => actor != null && CanAfford(actor.Actions, cost);

    /// <summary><see cref="CanAfford(CharacterController, ActionCostType)"/> on an action economy.</summary>
    public static bool CanAfford(ActionEconomy a, ActionCostType cost)
    {
        if (a == null)
            return false;
        switch (cost)
        {
            case ActionCostType.Free:
            case ActionCostType.NotAnAction:
                return true;
            case ActionCostType.Swift:
            case ActionCostType.Immediate:
                return a.HasSwiftAction;
            case ActionCostType.Move:
                return a.HasMoveAction || a.CanConvertStandardToMove;
            case ActionCostType.Standard:
                return a.HasStandardAction;
            case ActionCostType.FullRound:
                return a.HasFullRoundAction;
            default:
                return false;
        }
    }

    /// <summary>
    /// Pays <paramref name="cost"/>: the move action, else the standard action converted to a move; the standard action
    /// through CommitStandardAction; the full-round action; the swift action. False, and nothing spent, when it cannot.
    /// </summary>
    public static bool TryPay(CharacterController actor, ActionCostType cost)
    {
        if (!CanAfford(actor, cost))
            return false;
        ActionEconomy a = actor.Actions;
        switch (cost)
        {
            case ActionCostType.Free:
            case ActionCostType.NotAnAction:
                return true;
            case ActionCostType.Swift:
            case ActionCostType.Immediate:
                a.UseSwiftAction();
                return true;
            case ActionCostType.Move:
                if (a.HasMoveAction)
                    return a.UseMoveAction();
                a.ConvertStandardToMove();
                return true;
            case ActionCostType.Standard:
                return actor.CommitStandardAction();
            case ActionCostType.FullRound:
                a.UseFullRoundAction();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The catalog's action type of a cast's casting action (CastActionRules.CastingAction): a cast from a scroll, a wand
    /// or a held-charge delivery charges nothing at the cast (the item's or the delivery's own entry has its cost), so
    /// NotAnAction.
    /// </summary>
    public static ActionCostType From(CastActionCost cost)
    {
        switch (cost)
        {
            case CastActionCost.ItemOrDelivery: return ActionCostType.NotAnAction;
            case CastActionCost.Free: return ActionCostType.Free;
            case CastActionCost.Swift: return ActionCostType.Swift;
            case CastActionCost.Standard: return ActionCostType.Standard;
            case CastActionCost.FullRound: return ActionCostType.FullRound;
            default: return ActionCostType.Standard;
        }
    }

    /// <summary>The spent flags of <paramref name="actor"/>'s action economy, to compare after a run (<see cref="SpentSince"/>).</summary>
    public readonly struct Snapshot
    {
        public readonly bool Move, Standard, FullRound, Swift, Converted;

        /// <summary>A disabled or staggered turn (ActionEconomy.SingleActionOnly): its standard action also marks the move action used.</summary>
        public readonly bool SingleActionOnly;

        public Snapshot(ActionEconomy a)
        {
            Move = a != null && a.MoveActionUsed;
            Standard = a != null && a.StandardActionUsed;
            FullRound = a != null && a.FullRoundActionUsed;
            Swift = a != null && a.SwiftActionUsed;
            Converted = a != null && a.StandardConvertedToMove;
            SingleActionOnly = a != null && a.SingleActionOnly;
        }
    }

    public static Snapshot Take(CharacterController actor) => new Snapshot(actor != null ? actor.Actions : null);

    /// <summary>
    /// The largest action spent since <paramref name="before"/>: the full-round action (or the standard and move actions
    /// together), the standard action, a move action (or the converted standard), the swift action; Free when nothing.
    /// </summary>
    public static ActionCostType SpentSince(Snapshot before, CharacterController actor)
        => SpentBetween(before, Take(actor));

    /// <summary>
    /// The largest action spent between two snapshots (<see cref="SpentSince"/>). In a single-action turn the standard
    /// action also marks the move action used (ActionEconomy.UseStandardAction), so there both together read as Standard.
    /// </summary>
    public static ActionCostType SpentBetween(Snapshot before, Snapshot now)
    {
        bool fullRound = now.FullRound && !before.FullRound;
        bool standard = now.Standard && !before.Standard;
        bool move = (now.Move && !before.Move) || (now.Converted && !before.Converted);
        if (fullRound || (standard && move && !now.SingleActionOnly))
            return ActionCostType.FullRound;
        if (standard)
            return ActionCostType.Standard;
        if (move)
            return ActionCostType.Move;
        if (now.Swift && !before.Swift)
            return ActionCostType.Swift;
        return ActionCostType.Free;
    }
}
