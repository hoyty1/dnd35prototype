using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central event bus for decoupling game systems from UI and each other.
/// Follows the publish-subscribe pattern to allow loose coupling between
/// GameManager, services, and UI controllers.
/// 
/// Usage:
///   GameEventSystem.Instance.Publish(new CombatStartedEvent { Round = 1 });
///   GameEventSystem.Instance.Subscribe&lt;CombatStartedEvent&gt;(OnCombatStarted);
///   GameEventSystem.Instance.Unsubscribe&lt;CombatStartedEvent&gt;(OnCombatStarted);
/// </summary>
public class GameEventSystem
{
    // ─── Singleton ───────────────────────────────────────────────────
    private static GameEventSystem _instance;
    public static GameEventSystem Instance => _instance ??= new GameEventSystem();

    /// <summary>Reset for tests / scene reload.</summary>
    public static void Reset() => _instance = null;

    // ─── Internal Storage ────────────────────────────────────────────
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();
    private readonly Dictionary<Type, List<Delegate>> _oneTimeSubscribers = new Dictionary<Type, List<Delegate>>();

    // ─── Subscribe / Unsubscribe ─────────────────────────────────────

    /// <summary>Subscribe to an event type. Handler is called each time the event is published.</summary>
    public void Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
            _subscribers[type] = new List<Delegate>();
        _subscribers[type].Add(handler);
    }

    /// <summary>Subscribe to an event type for a single invocation, then auto-unsubscribe.</summary>
    public void SubscribeOnce<T>(Action<T> handler) where T : IGameEvent
    {
        var type = typeof(T);
        if (!_oneTimeSubscribers.ContainsKey(type))
            _oneTimeSubscribers[type] = new List<Delegate>();
        _oneTimeSubscribers[type].Add(handler);
    }

    /// <summary>Remove a subscription.</summary>
    public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var list))
            list.Remove(handler);
    }

    /// <summary>Remove all subscriptions (useful for scene transitions).</summary>
    public void ClearAll()
    {
        _subscribers.Clear();
        _oneTimeSubscribers.Clear();
    }

    // ─── Publish ─────────────────────────────────────────────────────

    /// <summary>Publish an event to all subscribers of its type.</summary>
    public void Publish<T>(T evt) where T : IGameEvent
    {
        var type = typeof(T);

        // Persistent subscribers
        if (_subscribers.TryGetValue(type, out var list))
        {
            // Iterate copy to allow modification during iteration
            var snapshot = new List<Delegate>(list);
            foreach (var d in snapshot)
            {
                try
                {
                    ((Action<T>)d).Invoke(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameEventSystem] Error in subscriber for {type.Name}: {ex}");
                }
            }
        }

        // One-time subscribers
        if (_oneTimeSubscribers.TryGetValue(type, out var oneTimeList) && oneTimeList.Count > 0)
        {
            var snapshot = new List<Delegate>(oneTimeList);
            oneTimeList.Clear();
            foreach (var d in snapshot)
            {
                try
                {
                    ((Action<T>)d).Invoke(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameEventSystem] Error in one-time subscriber for {type.Name}: {ex}");
                }
            }
        }
    }

    /// <summary>Returns count of persistent subscribers for a given event type.</summary>
    public int GetSubscriberCount<T>() where T : IGameEvent
    {
        return _subscribers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
    }
}

// ─── Marker Interface ────────────────────────────────────────────────
/// <summary>Marker interface for all game events published through GameEventSystem.</summary>
public interface IGameEvent { }

// ═══════════════════════════════════════════════════════════════════════
//  COMBAT FLOW EVENTS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Published when combat begins (initiative rolled, first turn about to start).</summary>
public struct CombatStartedEvent : IGameEvent
{
    public int TotalCombatants;
    public int Round;
}

/// <summary>Published when combat ends (victory or defeat).</summary>
public struct CombatEndedEvent : IGameEvent
{
    public bool PlayerVictory;
    public string Context;
}

/// <summary>Published when a new combat round begins.</summary>
public struct NewRoundEvent : IGameEvent
{
    public int RoundNumber;
}

/// <summary>Published when a character's turn begins.</summary>
public struct TurnStartedEvent : IGameEvent
{
    public CharacterController Character;
    public bool IsPC;
    public int RoundNumber;
}

/// <summary>Published when a character's turn ends.</summary>
public struct TurnEndedEvent : IGameEvent
{
    public CharacterController Character;
}

// ═══════════════════════════════════════════════════════════════════════
//  CHARACTER STATE EVENTS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Published when a character takes damage.</summary>
public struct DamageTakenEvent : IGameEvent
{
    public CharacterController Target;
    public CharacterController Source;
    public int Amount;
    public string DamageType;
}

/// <summary>Published when a character is healed.</summary>
public struct HealingReceivedEvent : IGameEvent
{
    public CharacterController Target;
    public int Amount;
    public string Source;
}

/// <summary>Published when a character is defeated (HP <= 0 and dead).</summary>
public struct CharacterDefeatedEvent : IGameEvent
{
    public CharacterController Character;
    public CharacterController Killer;
    public string Context;
}

/// <summary>Published when a condition is applied to a character.</summary>
public struct ConditionAppliedEvent : IGameEvent
{
    public CharacterController Target;
    public CombatConditionType ConditionType;
    public int DurationRounds;
}

/// <summary>Published when a condition is removed from a character.</summary>
public struct ConditionRemovedEvent : IGameEvent
{
    public CharacterController Target;
    public CombatConditionType ConditionType;
    public string Reason;
}

// ═══════════════════════════════════════════════════════════════════════
//  COMBAT ACTION EVENTS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Published when an attack is resolved (hit or miss).</summary>
public struct AttackResolvedEvent : IGameEvent
{
    public CharacterController Attacker;
    public CharacterController Target;
    public bool Hit;
    public int Damage;
    public bool IsCritical;
    public string WeaponName;
}

/// <summary>Published when a spell is cast.</summary>
public struct SpellCastEvent : IGameEvent
{
    public CharacterController Caster;
    public CharacterController Target;
    public string SpellName;
    public string SpellId;
    public int SpellLevel;
    public bool IsAoE;
    public bool WasCountered;
}

/// <summary>Published when a special attack is performed (grapple, trip, disarm, etc.).</summary>
public struct SpecialAttackEvent : IGameEvent
{
    public CharacterController Attacker;
    public CharacterController Target;
    public string AttackType;
    public bool Success;
}

// ═══════════════════════════════════════════════════════════════════════
//  ECONOMY / INVENTORY EVENTS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Published when party gold changes.</summary>
public struct GoldChangedEvent : IGameEvent
{
    public int OldAmount;
    public int NewAmount;
    public string Reason;
}

/// <summary>Published when an item is equipped or unequipped.</summary>
public struct ItemEquippedEvent : IGameEvent
{
    public CharacterController Character;
    public string ItemName;
    public string Slot;
    public bool Equipped; // true = equipped, false = unequipped
}

/// <summary>Published when a consumable is used (potion, scroll, etc.).</summary>
public struct ConsumableUsedEvent : IGameEvent
{
    public CharacterController User;
    public string ItemName;
    public string Effect;
}

// ═══════════════════════════════════════════════════════════════════════
//  MOVEMENT EVENTS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Published when a character moves on the grid.</summary>
public struct CharacterMovedEvent : IGameEvent
{
    public CharacterController Character;
    public Vector2Int From;
    public Vector2Int To;
    public int SquaresMoved;
    public bool ProvokedAoO;
}

// ═══════════════════════════════════════════════════════════════════════
//  UI STATE EVENTS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Published when the UI phase changes (pre-combat, combat, loot, etc.).</summary>
public struct UIPhaseChangedEvent : IGameEvent
{
    public string Phase; // "PreCombat", "Combat", "LootCollection", "EncounterSelection"
}

/// <summary>Published when action choices should be displayed for a PC.</summary>
public struct ShowActionChoicesEvent : IGameEvent
{
    public CharacterController Character;
    public bool HasStandardAction;
    public bool HasMoveAction;
    public bool HasSwiftAction;
}

/// <summary>Published to request a UI stats refresh for all characters.</summary>
public struct StatsUIRefreshRequestedEvent : IGameEvent { }
