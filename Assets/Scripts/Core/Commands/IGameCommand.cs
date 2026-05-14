using System.Collections;

/// <summary>
/// Interface for all game commands. Implements the Command pattern
/// to encapsulate player/AI actions as objects, enabling:
/// - Consistent action execution pipeline
/// - Validation before execution
/// - Future undo/redo support
/// - Action queueing for AI turns
/// - Logging and replay
/// </summary>
public interface IGameCommand
{
    /// <summary>Display name for logging and UI feedback.</summary>
    string DisplayName { get; }

    /// <summary>The character performing this action.</summary>
    CharacterController Actor { get; }

    /// <summary>Validate whether this command can be executed right now.</summary>
    /// <param name="reason">If invalid, the reason why.</param>
    /// <returns>True if the command can be executed.</returns>
    bool CanExecute(out string reason);

    /// <summary>Execute the command. May trigger UI updates, animations, etc.</summary>
    void Execute();

    /// <summary>
    /// The action cost type for this command.
    /// Used by the action economy to track what actions have been used.
    /// </summary>
    ActionCostType ActionCost { get; }
}

/// <summary>
/// Extended interface for commands that use coroutines for execution
/// (e.g., movement animations, AoO resolution).
/// </summary>
public interface IGameCommandAsync : IGameCommand
{
    /// <summary>Execute the command as a coroutine (for animated/async actions).</summary>
    IEnumerator ExecuteAsync();
}

/// <summary>Action cost types for D&amp;D 3.5e action economy.</summary>
public enum ActionCostType
{
    Free,           // Free action (no cost)
    Swift,          // Swift action (1 per turn)
    Move,           // Move action
    Standard,       // Standard action
    FullRound,      // Full-round action (replaces move + standard)
    Immediate,      // Immediate action (uses swift, can interrupt)
    NotAnAction     // Not a game action (UI-only, query, etc.)
}
