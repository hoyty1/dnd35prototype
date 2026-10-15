using System;
using UnityEngine;

/// <summary>
/// Tracks Paladin Lay on Hands healing pool (D&D 3.5e PHB p.44).
///
/// From 2nd level; healing per day = paladin level x Charisma bonus (Charisma 12 or higher).
/// The pool heals creatures or, after a successful melee touch attack, damages undead (no saving throw).
/// The rule itself (targets, touch attack, PCs and NPCs alike) is LayOnHandsRules; this class only counts the pool.
/// </summary>
[Serializable]
public class LayOnHandsData
{
    [SerializeField] private int _paladinLevel;
    [SerializeField] private int _charismaModifier;
    [SerializeField] private int _poolUsed;

    /// <summary>Current paladin level.</summary>
    public int PaladinLevel => _paladinLevel;

    /// <summary>CHA modifier for pool calculation.</summary>
    public int CharismaModifier => _charismaModifier;

    /// <summary>
    /// Maximum healing pool per day (PHB p.44).
    /// Equal to Paladin level × Charisma modifier.
    /// Returns 0 below 2nd level or without a Charisma bonus (PaladinClass.LayOnHandsPool).
    /// </summary>
    public int MaxPool
    {
        get
        {
            return PaladinClass.LayOnHandsPool(_paladinLevel, _charismaModifier);
        }
    }

    /// <summary>How many HP have been spent from the pool today.</summary>
    public int PoolUsed => _poolUsed;

    /// <summary>Remaining HP in the healing pool.</summary>
    public int RemainingPool => Mathf.Max(0, MaxPool - _poolUsed);

    /// <summary>Whether the paladin can lay on hands (has pool remaining).</summary>
    public bool CanLayOnHands => RemainingPool > 0;

    /// <summary>Initialize or update when level or stats change.</summary>
    public void Initialize(int paladinLevel, int charismaModifier)
    {
        _paladinLevel = paladinLevel;
        _charismaModifier = charismaModifier;
    }

    /// <summary>
    /// Heal a living creature for up to the specified amount (capped by remaining pool).
    /// PHB p.44: The paladin can divide healing among multiple recipients.
    /// Returns actual HP healed.
    /// </summary>
    public int HealLiving(int amount)
    {
        if (!CanLayOnHands || amount <= 0) return 0;

        int actual = Mathf.Min(amount, RemainingPool);
        _poolUsed += actual;
        Debug.Log($"[Paladin] Lay on Hands: Healed {actual} HP. ({RemainingPool}/{MaxPool} pool remaining)");
        return actual;
    }

    /// <summary>
    /// Deal damage to an undead creature using lay on hands (after a successful melee touch attack).
    /// PHB p.44: the paladin spends the points she chooses as damage; there is no saving throw.
    /// Returns the points spent.
    /// </summary>
    public int HarmUndead(int amount)
    {
        if (!CanLayOnHands || amount <= 0) return 0;

        int actual = Mathf.Min(amount, RemainingPool);
        _poolUsed += actual;
        Debug.Log($"[Paladin] Lay on Hands (harm undead): {actual} damage. ({RemainingPool}/{MaxPool} pool remaining)");
        return actual;
    }

    /// <summary>Reset healing pool (called on long rest / daily refresh).</summary>
    public void RefreshPool()
    {
        _poolUsed = 0;
    }
}
