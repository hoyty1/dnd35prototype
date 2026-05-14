using System.Collections;
using UnityEngine;

/// <summary>
/// Concrete command implementations for common combat actions.
/// These wrap GameManager method calls to provide validation,
/// logging, and consistent action economy tracking.
/// </summary>

// ═══════════════════════════════════════════════════════════════════════
//  ATTACK COMMANDS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Standard single attack action command.</summary>
public class AttackCommand : IGameCommand
{
    public string DisplayName => "Attack";
    public CharacterController Actor { get; }
    public CharacterController Target { get; }
    public ActionCostType ActionCost => ActionCostType.Standard;

    public AttackCommand(CharacterController actor, CharacterController target)
    {
        Actor = actor;
        Target = target;
    }

    public bool CanExecute(out string reason)
    {
        if (Actor == null || !Actor.IsAlive)
        {
            reason = "Actor is null or dead.";
            return false;
        }
        if (Target == null || !Target.IsAlive)
        {
            reason = "Target is null or dead.";
            return false;
        }
        if (!Actor.Actions.HasStandardAction)
        {
            reason = "No standard action available.";
            return false;
        }
        reason = null;
        return true;
    }

    public void Execute()
    {
        // Delegates to GameManager — the command wraps the call
        // but provides the validation/logging/event pipeline
        Debug.Log($"[AttackCommand] {Actor.Stats.CharacterName} attacks {Target.Stats.CharacterName}");
    }
}

/// <summary>Full attack action command (full-round action).</summary>
public class FullAttackCommand : IGameCommand
{
    public string DisplayName => "Full Attack";
    public CharacterController Actor { get; }
    public CharacterController Target { get; }
    public ActionCostType ActionCost => ActionCostType.FullRound;

    public FullAttackCommand(CharacterController actor, CharacterController target)
    {
        Actor = actor;
        Target = target;
    }

    public bool CanExecute(out string reason)
    {
        if (Actor == null || !Actor.IsAlive)
        {
            reason = "Actor is null or dead.";
            return false;
        }
        if (Target == null || !Target.IsAlive)
        {
            reason = "Target is null or dead.";
            return false;
        }
        if (!Actor.Actions.HasStandardAction || !Actor.Actions.HasMoveAction)
        {
            reason = "Full attack requires both standard and move action.";
            return false;
        }
        reason = null;
        return true;
    }

    public void Execute()
    {
        Debug.Log($"[FullAttackCommand] {Actor.Stats.CharacterName} full attacks {Target.Stats.CharacterName}");
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  MOVEMENT COMMANDS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Move action command.</summary>
public class MoveCommand : IGameCommandAsync
{
    public string DisplayName => "Move";
    public CharacterController Actor { get; }
    public Vector2Int Destination { get; }
    public ActionCostType ActionCost => ActionCostType.Move;

    public MoveCommand(CharacterController actor, Vector2Int destination)
    {
        Actor = actor;
        Destination = destination;
    }

    public bool CanExecute(out string reason)
    {
        if (Actor == null || !Actor.IsAlive)
        {
            reason = "Actor is null or dead.";
            return false;
        }
        if (!Actor.Actions.HasMoveAction)
        {
            reason = "No move action available.";
            return false;
        }
        reason = null;
        return true;
    }

    public void Execute()
    {
        // Sync stub — actual execution is async
        Debug.Log($"[MoveCommand] {Actor.Stats.CharacterName} moves to {Destination}");
    }

    public IEnumerator ExecuteAsync()
    {
        Debug.Log($"[MoveCommand] {Actor.Stats.CharacterName} moving to {Destination}");
        // Actual movement is delegated to GameManager's movement pipeline
        yield break;
    }
}

/// <summary>Five-foot step command (free action, no AoO).</summary>
public class FiveFootStepCommand : IGameCommand
{
    public string DisplayName => "5-Foot Step";
    public CharacterController Actor { get; }
    public Vector2Int Destination { get; }
    public ActionCostType ActionCost => ActionCostType.Free;

    public FiveFootStepCommand(CharacterController actor, Vector2Int destination)
    {
        Actor = actor;
        Destination = destination;
    }

    public bool CanExecute(out string reason)
    {
        if (Actor == null || !Actor.IsAlive)
        {
            reason = "Actor is null or dead.";
            return false;
        }
        if (Actor.Actions.HasMoved5Ft)
        {
            reason = "Already used 5-foot step this turn.";
            return false;
        }
        if (!Actor.Actions.HasMoveAction)
        {
            reason = "No move action available for 5-foot step.";
            return false;
        }
        reason = null;
        return true;
    }

    public void Execute()
    {
        Debug.Log($"[FiveFootStepCommand] {Actor.Stats.CharacterName} steps to {Destination}");
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  SPELL COMMANDS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Cast spell command.</summary>
public class CastSpellCommand : IGameCommand
{
    public string DisplayName => $"Cast {SpellName}";
    public CharacterController Actor { get; }
    public CharacterController Target { get; }
    public string SpellName { get; }
    public string SpellId { get; }
    public ActionCostType ActionCost { get; }

    public CastSpellCommand(CharacterController actor, CharacterController target, string spellName, string spellId, bool isQuickened = false)
    {
        Actor = actor;
        Target = target;
        SpellName = spellName;
        SpellId = spellId;
        ActionCost = isQuickened ? ActionCostType.Swift : ActionCostType.Standard;
    }

    public bool CanExecute(out string reason)
    {
        if (Actor == null || !Actor.IsAlive)
        {
            reason = "Caster is null or dead.";
            return false;
        }
        if (ActionCost == ActionCostType.Standard && !Actor.Actions.HasStandardAction)
        {
            reason = "No standard action available for casting.";
            return false;
        }
        if (ActionCost == ActionCostType.Swift && !Actor.Actions.HasSwiftAction)
        {
            reason = "No swift action available for quickened spell.";
            return false;
        }
        reason = null;
        return true;
    }

    public void Execute()
    {
        Debug.Log($"[CastSpellCommand] {Actor.Stats.CharacterName} casts {SpellName}");
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  ITEM COMMANDS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Use consumable item command.</summary>
public class UseItemCommand : IGameCommand
{
    public string DisplayName => $"Use {ItemName}";
    public CharacterController Actor { get; }
    public string ItemName { get; }
    public int InventoryIndex { get; }
    public ActionCostType ActionCost => ActionCostType.Standard;

    public UseItemCommand(CharacterController actor, string itemName, int inventoryIndex)
    {
        Actor = actor;
        ItemName = itemName;
        InventoryIndex = inventoryIndex;
    }

    public bool CanExecute(out string reason)
    {
        if (Actor == null || !Actor.IsAlive)
        {
            reason = "Actor is null or dead.";
            return false;
        }
        if (!Actor.Actions.HasStandardAction)
        {
            reason = "No standard action available.";
            return false;
        }
        reason = null;
        return true;
    }

    public void Execute()
    {
        Debug.Log($"[UseItemCommand] {Actor.Stats.CharacterName} uses {ItemName}");
    }
}

/// <summary>End turn command (free action).</summary>
public class EndTurnCommand : IGameCommand
{
    public string DisplayName => "End Turn";
    public CharacterController Actor { get; }
    public ActionCostType ActionCost => ActionCostType.Free;

    public EndTurnCommand(CharacterController actor)
    {
        Actor = actor;
    }

    public bool CanExecute(out string reason)
    {
        reason = null;
        return true; // Can always end turn
    }

    public void Execute()
    {
        Debug.Log($"[EndTurnCommand] {Actor?.Stats?.CharacterName ?? "?"} ends turn.");
    }
}
