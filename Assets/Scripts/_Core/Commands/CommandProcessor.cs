using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central processor for game commands. Validates, executes, and logs all
/// player/AI actions through a unified pipeline.
/// 
/// Benefits:
/// - Single entry point for all actions → consistent validation and logging
/// - Decouples "what" (command) from "when/how" (processor)
/// - Action history for debugging and future replay
/// - Can queue commands for AI batch execution
/// </summary>
public class CommandProcessor : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────
    public static CommandProcessor Instance { get; private set; }

    // ─── Configuration ───────────────────────────────────────────────
    [SerializeField] private int _maxHistorySize = 200;

    // ─── State ───────────────────────────────────────────────────────
    private readonly List<CommandRecord> _history = new List<CommandRecord>();
    private readonly Queue<IGameCommand> _commandQueue = new Queue<IGameCommand>();
    private bool _isProcessing;

    // ─── Events ──────────────────────────────────────────────────────
    /// <summary>Fired before a command is executed. Return false to cancel.</summary>
    public event Func<IGameCommand, bool> OnBeforeExecute;

    /// <summary>Fired after a command completes execution.</summary>
    public event Action<IGameCommand, bool> OnAfterExecute;

    // ─── Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    // ─── Execute Commands ────────────────────────────────────────────

    /// <summary>
    /// Execute a command immediately. Validates first, then runs.
    /// Returns true if the command was executed successfully.
    /// </summary>
    public bool Execute(IGameCommand command)
    {
        if (command == null)
        {
            Debug.LogError("[CommandProcessor] Attempted to execute null command.");
            return false;
        }

        // Validate
        if (!command.CanExecute(out string reason))
        {
            Debug.Log($"[CommandProcessor] Command '{command.DisplayName}' rejected: {reason}");
            return false;
        }

        // Pre-execute hook
        if (OnBeforeExecute != null)
        {
            foreach (var handler in OnBeforeExecute.GetInvocationList())
            {
                if (!(bool)handler.DynamicInvoke(command))
                {
                    Debug.Log($"[CommandProcessor] Command '{command.DisplayName}' cancelled by pre-execute hook.");
                    return false;
                }
            }
        }

        // Execute
        bool success = true;
        try
        {
            command.Execute();
            RecordCommand(command, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandProcessor] Error executing '{command.DisplayName}': {ex}");
            RecordCommand(command, false);
            success = false;
        }

        // Post-execute hook
        OnAfterExecute?.Invoke(command, success);

        // Publish event
        GameEventSystem.Instance.Publish(new CommandExecutedEvent
        {
            Command = command,
            Success = success
        });

        return success;
    }

    /// <summary>
    /// Execute an async command as a coroutine (for movement, animations, etc.).
    /// </summary>
    public Coroutine ExecuteAsync(IGameCommandAsync command)
    {
        if (command == null)
        {
            Debug.LogError("[CommandProcessor] Attempted to execute null async command.");
            return null;
        }

        if (!command.CanExecute(out string reason))
        {
            Debug.Log($"[CommandProcessor] Async command '{command.DisplayName}' rejected: {reason}");
            return null;
        }

        return StartCoroutine(ExecuteAsyncInternal(command));
    }

    private IEnumerator ExecuteAsyncInternal(IGameCommandAsync command)
    {
        _isProcessing = true;

        bool success = true;
        Exception caughtEx = null;

        // Cannot yield inside try-catch in C#, so we wrap with a helper
        var enumerator = command.ExecuteAsync();
        while (true)
        {
            bool moveNext;
            try
            {
                moveNext = enumerator.MoveNext();
            }
            catch (Exception ex)
            {
                caughtEx = ex;
                break;
            }
            if (!moveNext) break;
            yield return enumerator.Current;
        }

        if (caughtEx != null)
        {
            Debug.LogError($"[CommandProcessor] Error in async command '{command.DisplayName}': {caughtEx}");
            RecordCommand(command, false);
            success = false;
        }
        else
        {
            RecordCommand(command, true);
        }

        _isProcessing = false;

        OnAfterExecute?.Invoke(command, success);

        GameEventSystem.Instance.Publish(new CommandExecutedEvent
        {
            Command = command,
            Success = success
        });
    }

    // ─── Queue ───────────────────────────────────────────────────────

    /// <summary>Queue a command for later execution (useful for AI batching).</summary>
    public void Enqueue(IGameCommand command)
    {
        _commandQueue.Enqueue(command);
    }

    /// <summary>Execute all queued commands in order.</summary>
    public IEnumerator ProcessQueue()
    {
        while (_commandQueue.Count > 0)
        {
            var cmd = _commandQueue.Dequeue();
            if (cmd is IGameCommandAsync asyncCmd)
            {
                yield return ExecuteAsyncInternal(asyncCmd);
            }
            else
            {
                Execute(cmd);
            }
            yield return null; // one frame between commands
        }
    }

    /// <summary>Clear the command queue without executing.</summary>
    public void ClearQueue()
    {
        _commandQueue.Clear();
    }

    // ─── History ─────────────────────────────────────────────────────

    /// <summary>Record a command in the history.</summary>
    private void RecordCommand(IGameCommand command, bool success)
    {
        _history.Add(new CommandRecord
        {
            CommandName = command.DisplayName,
            ActorName = command.Actor?.Stats?.CharacterName ?? "Unknown",
            ActionCost = command.ActionCost,
            Success = success,
            Timestamp = Time.time
        });

        // Trim history
        while (_history.Count > _maxHistorySize)
            _history.RemoveAt(0);
    }

    /// <summary>Get the last N commands from history.</summary>
    public List<CommandRecord> GetRecentHistory(int count = 10)
    {
        int start = Mathf.Max(0, _history.Count - count);
        return _history.GetRange(start, _history.Count - start);
    }

    /// <summary>Whether a command is currently being processed.</summary>
    public bool IsProcessing => _isProcessing;

    /// <summary>Number of commands in the queue.</summary>
    public int QueuedCount => _commandQueue.Count;

    // ─── Data Structures ─────────────────────────────────────────────

    public struct CommandRecord
    {
        public string CommandName;
        public string ActorName;
        public ActionCostType ActionCost;
        public bool Success;
        public float Timestamp;

        public override string ToString()
            => $"[{Timestamp:F1}s] {ActorName}: {CommandName} ({ActionCost}) - {(Success ? "OK" : "FAIL")}";
    }
}

/// <summary>Published when any command is executed through the CommandProcessor.</summary>
public struct CommandExecutedEvent : IGameEvent
{
    public IGameCommand Command;
    public bool Success;
}
