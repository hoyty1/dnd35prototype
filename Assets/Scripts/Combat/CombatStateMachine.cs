using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// State machine for managing combat flow phases.
/// Reduces complex if/else chains in GameManager by modeling
/// combat as explicit states with defined transitions.
/// 
/// States:
///   Idle → EncounterSetup → PreCombat → InitiativeRoll → 
///   PlayerTurn → EnemyTurn → Victory → Defeat → LootCollection → Idle
/// </summary>
public class CombatStateMachine
{
    // ─── State Enum ──────────────────────────────────────────────────
    public enum CombatState
    {
        Idle,               // No combat active, waiting for encounter selection
        EncounterSetup,     // Setting up enemies, grid, etc.
        PreCombat,          // Pre-combat hub (inventory, spells, store)
        InitiativeRoll,     // Rolling initiative, building turn order
        RoundStart,         // New round beginning, processing round-start effects
        PlayerTurn,         // A PC is taking their turn
        EnemyTurn,          // An NPC is executing AI turn
        SummonTurn,         // A summoned creature is executing its turn
        AwaitingPlayerInput,// Waiting for player to choose action, target, or movement
        ResolvingAction,    // An action is being resolved (attack, spell, movement animation)
        Victory,            // All enemies defeated
        Defeat,             // All PCs defeated
        LootCollection,     // Post-combat loot screen
        PostCombat          // Returning to encounter selection / hub
    }

    // ─── Sub-State for Awaiting Input ────────────────────────────────
    public enum PlayerInputSubState
    {
        None,
        ChoosingAction,         // Main action menu
        SelectingAttackTarget,  // Choosing target for attack
        SelectingSpellTarget,   // Choosing target for spell
        SelectingMovement,      // Choosing movement destination
        SelectingAoEPlacement,  // Placing AoE spell
        SelectingSpecialTarget, // Choosing target for special attack
        PlacingSummon,          // Placing a summoned creature
        ViewingInventory,       // Inventory/store open during turn
        ConfirmingAction        // AoO confirmation prompt, etc.
    }

    // ─── Events ──────────────────────────────────────────────────────
    /// <summary>Fired when combat state changes. Provides old and new state.</summary>
    public event Action<CombatState, CombatState> OnStateChanged;

    /// <summary>Fired when player input sub-state changes.</summary>
    public event Action<PlayerInputSubState, PlayerInputSubState> OnInputSubStateChanged;

    // ─── Current State ───────────────────────────────────────────────
    public CombatState CurrentState { get; private set; } = CombatState.Idle;
    public CombatState PreviousState { get; private set; } = CombatState.Idle;
    public PlayerInputSubState CurrentInputSubState { get; private set; } = PlayerInputSubState.None;

    /// <summary>The character whose turn is currently active (PC or NPC).</summary>
    public CharacterController ActiveCharacter { get; set; }

    /// <summary>Current combat round number.</summary>
    public int CurrentRound { get; set; }

    /// <summary>Time spent in current state (for timeout/animation purposes).</summary>
    public float TimeInCurrentState { get; private set; }

    // ─── Valid Transitions ───────────────────────────────────────────
    private static readonly Dictionary<CombatState, HashSet<CombatState>> ValidTransitions = new Dictionary<CombatState, HashSet<CombatState>>
    {
        { CombatState.Idle, new HashSet<CombatState> { CombatState.EncounterSetup, CombatState.PreCombat } },
        { CombatState.EncounterSetup, new HashSet<CombatState> { CombatState.PreCombat, CombatState.InitiativeRoll } },
        { CombatState.PreCombat, new HashSet<CombatState> { CombatState.InitiativeRoll, CombatState.Idle } },
        { CombatState.InitiativeRoll, new HashSet<CombatState> { CombatState.RoundStart } },
        { CombatState.RoundStart, new HashSet<CombatState> { CombatState.PlayerTurn, CombatState.EnemyTurn, CombatState.SummonTurn } },
        { CombatState.PlayerTurn, new HashSet<CombatState> { CombatState.AwaitingPlayerInput, CombatState.ResolvingAction, CombatState.EnemyTurn, CombatState.SummonTurn, CombatState.RoundStart, CombatState.Victory, CombatState.Defeat } },
        { CombatState.EnemyTurn, new HashSet<CombatState> { CombatState.ResolvingAction, CombatState.PlayerTurn, CombatState.SummonTurn, CombatState.EnemyTurn, CombatState.RoundStart, CombatState.Victory, CombatState.Defeat } },
        { CombatState.SummonTurn, new HashSet<CombatState> { CombatState.ResolvingAction, CombatState.PlayerTurn, CombatState.EnemyTurn, CombatState.SummonTurn, CombatState.RoundStart, CombatState.Victory, CombatState.Defeat } },
        { CombatState.AwaitingPlayerInput, new HashSet<CombatState> { CombatState.ResolvingAction, CombatState.PlayerTurn, CombatState.AwaitingPlayerInput, CombatState.Victory, CombatState.Defeat } },
        { CombatState.ResolvingAction, new HashSet<CombatState> { CombatState.PlayerTurn, CombatState.EnemyTurn, CombatState.SummonTurn, CombatState.AwaitingPlayerInput, CombatState.RoundStart, CombatState.Victory, CombatState.Defeat } },
        { CombatState.Victory, new HashSet<CombatState> { CombatState.LootCollection, CombatState.PostCombat, CombatState.Idle } },
        { CombatState.Defeat, new HashSet<CombatState> { CombatState.PostCombat, CombatState.Idle } },
        { CombatState.LootCollection, new HashSet<CombatState> { CombatState.PostCombat, CombatState.Idle } },
        { CombatState.PostCombat, new HashSet<CombatState> { CombatState.Idle, CombatState.EncounterSetup, CombatState.PreCombat } },
    };

    // ─── State Transition ────────────────────────────────────────────

    /// <summary>
    /// Transition to a new combat state. Validates the transition is legal.
    /// Returns true if transition succeeded.
    /// </summary>
    public bool TransitionTo(CombatState newState)
    {
        if (CurrentState == newState)
            return true; // no-op

        if (!IsValidTransition(CurrentState, newState))
        {
            Debug.LogWarning($"[CombatStateMachine] Invalid transition: {CurrentState} → {newState}");
            return false;
        }

        PreviousState = CurrentState;
        CurrentState = newState;
        TimeInCurrentState = 0f;

        // Reset input sub-state when leaving player input
        if (newState != CombatState.AwaitingPlayerInput && CurrentInputSubState != PlayerInputSubState.None)
        {
            SetInputSubState(PlayerInputSubState.None);
        }

        Debug.Log($"[CombatStateMachine] {PreviousState} → {CurrentState}");
        OnStateChanged?.Invoke(PreviousState, newState);

        // Publish through GameEventSystem
        GameEventSystem.Instance.Publish(new CombatStateChangedEvent
        {
            OldState = PreviousState,
            NewState = newState
        });

        return true;
    }

    /// <summary>
    /// Force a state transition without validation (for error recovery).
    /// </summary>
    public void ForceState(CombatState state)
    {
        PreviousState = CurrentState;
        CurrentState = state;
        TimeInCurrentState = 0f;
        Debug.LogWarning($"[CombatStateMachine] FORCED: {PreviousState} → {CurrentState}");
        OnStateChanged?.Invoke(PreviousState, state);
    }

    /// <summary>Set the player input sub-state (only valid during AwaitingPlayerInput).</summary>
    public void SetInputSubState(PlayerInputSubState subState)
    {
        if (CurrentState != CombatState.AwaitingPlayerInput && subState != PlayerInputSubState.None)
        {
            Debug.LogWarning($"[CombatStateMachine] Cannot set input sub-state {subState} outside AwaitingPlayerInput (current: {CurrentState})");
            return;
        }

        var old = CurrentInputSubState;
        CurrentInputSubState = subState;
        if (old != subState)
            OnInputSubStateChanged?.Invoke(old, subState);
    }

    // ─── Queries ─────────────────────────────────────────────────────

    /// <summary>Is combat currently active (not idle/post-combat)?</summary>
    public bool IsCombatActive =>
        CurrentState != CombatState.Idle &&
        CurrentState != CombatState.PostCombat &&
        CurrentState != CombatState.PreCombat &&
        CurrentState != CombatState.EncounterSetup;

    /// <summary>Is it currently a player's turn and awaiting input?</summary>
    public bool IsAwaitingPlayerInput => CurrentState == CombatState.AwaitingPlayerInput;

    /// <summary>Is an action currently being resolved (animation/effect in progress)?</summary>
    public bool IsResolving => CurrentState == CombatState.ResolvingAction;

    /// <summary>Is combat over (victory or defeat)?</summary>
    public bool IsCombatOver => CurrentState == CombatState.Victory || CurrentState == CombatState.Defeat;

    /// <summary>Can the player interact with the UI right now?</summary>
    public bool CanAcceptPlayerInput =>
        CurrentState == CombatState.AwaitingPlayerInput ||
        CurrentState == CombatState.PlayerTurn;

    /// <summary>Check if a transition from current state to target is valid.</summary>
    public bool IsValidTransition(CombatState from, CombatState to)
    {
        return ValidTransitions.TryGetValue(from, out var valid) && valid.Contains(to);
    }

    /// <summary>Update timer. Call from GameManager.Update().</summary>
    public void Tick(float deltaTime)
    {
        TimeInCurrentState += deltaTime;
    }

    /// <summary>Reset to idle state.</summary>
    public void Reset()
    {
        PreviousState = CurrentState;
        CurrentState = CombatState.Idle;
        CurrentInputSubState = PlayerInputSubState.None;
        ActiveCharacter = null;
        CurrentRound = 0;
        TimeInCurrentState = 0f;
    }

    /// <summary>Get a string summary of current state for debugging.</summary>
    public override string ToString()
    {
        var sub = CurrentInputSubState != PlayerInputSubState.None ? $" [{CurrentInputSubState}]" : "";
        var chr = ActiveCharacter != null ? $" ({ActiveCharacter.Stats?.CharacterName ?? "?"})" : "";
        return $"CombatState: {CurrentState}{sub}{chr} R{CurrentRound}";
    }
}

// ─── Event for state machine changes ─────────────────────────────────
public struct CombatStateChangedEvent : IGameEvent
{
    public CombatStateMachine.CombatState OldState;
    public CombatStateMachine.CombatState NewState;
}
