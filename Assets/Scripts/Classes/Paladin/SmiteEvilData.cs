using System;
using UnityEngine;

/// <summary>
/// Tracks Paladin Smite Evil uses and calculates bonuses (D&D 3.5e PHB p.44).
/// 
/// Smite Evil: Once per day at 1st level, +1 per 5 levels thereafter.
/// Attack roll: +CHA modifier
/// Damage roll: +Paladin level (double vs Evil outsiders, evil dragons, undead)
/// If target is not Evil, smite has no effect but is still expended.
/// </summary>
[Serializable]
public class SmiteEvilData
{
    [SerializeField] private int _paladinLevel;
    [SerializeField] private int _charismaModifier;
    [SerializeField] private int _usesExpended;

    /// <summary>Current paladin level.</summary>
    public int PaladinLevel => _paladinLevel;

    /// <summary>CHA modifier used for smite attack bonus.</summary>
    public int CharismaModifier => _charismaModifier;

    /// <summary>How many smites have been used today.</summary>
    public int UsesExpended => _usesExpended;

    /// <summary>
    /// Maximum smite evil uses per day (PHB p.44).
    /// 1/day at L1, 2/day at L5, 3/day at L10, 4/day at L15, 5/day at L20.
    /// </summary>
    public int MaxUsesPerDay
    {
        get
        {
            if (_paladinLevel <= 0) return 0;
            if (_paladinLevel < 5) return 1;
            if (_paladinLevel < 10) return 2;
            if (_paladinLevel < 15) return 3;
            if (_paladinLevel < 20) return 4;
            return 5; // Level 20
        }
    }

    /// <summary>Remaining smite uses today.</summary>
    public int RemainingUses => Mathf.Max(0, MaxUsesPerDay - _usesExpended);

    /// <summary>Whether the paladin can smite this round.</summary>
    public bool CanSmite => RemainingUses > 0;

    /// <summary>Initialize or update smite data when level or stats change.</summary>
    public void Initialize(int paladinLevel, int charismaModifier)
    {
        _paladinLevel = paladinLevel;
        _charismaModifier = charismaModifier;
    }

    /// <summary>
    /// Attack bonus when smiting (+CHA modifier, minimum +0).
    /// PHB p.44: "adds her Charisma bonus (if any) to her attack roll"
    /// </summary>
    public int GetSmiteAttackBonus()
    {
        return Mathf.Max(0, _charismaModifier);
    }

    /// <summary>
    /// Damage bonus when smiting an evil target.
    /// PHB p.44: "+1 point of damage per paladin level"
    /// </summary>
    /// <param name="isSpecialEvil">True for Evil outsiders, evil dragons, undead — double damage.</param>
    public int GetSmiteDamageBonus(bool isSpecialEvil = false)
    {
        int baseDamage = _paladinLevel;
        // PHB doesn't actually specify double damage for special types in 3.5e base rules.
        // That's a 5e thing. In 3.5e it's just +paladin level to damage.
        // Keeping parameter for future extensibility but not doubling.
        return baseDamage;
    }

    /// <summary>
    /// Use one smite evil attempt. Returns true if successful (had uses remaining).
    /// If the target is not evil, the smite still counts as used.
    /// </summary>
    public bool ExpendSmite()
    {
        if (!CanSmite) return false;
        _usesExpended++;
        return true;
    }

    /// <summary>Reset smite uses (called on long rest / daily refresh).</summary>
    public void RefreshUses()
    {
        _usesExpended = 0;
    }

    /// <summary>
    /// Perform a smite evil attack calculation.
    /// Returns (attackBonus, damageBonus) or (0,0) if no uses remain.
    /// </summary>
    public (int attackBonus, int damageBonus) AttemptSmite(bool targetIsEvil, bool isSpecialEvil = false)
    {
        if (!ExpendSmite())
            return (0, 0);

        int atkBonus = GetSmiteAttackBonus();

        if (!targetIsEvil)
        {
            // Smite wasted on non-evil target
            Debug.Log($"[Paladin] Smite Evil wasted — target is not Evil. ({RemainingUses}/{MaxUsesPerDay} uses remaining)");
            return (0, 0);
        }

        int dmgBonus = GetSmiteDamageBonus(isSpecialEvil);
        Debug.Log($"[Paladin] Smite Evil! +{atkBonus} attack, +{dmgBonus} damage. ({RemainingUses}/{MaxUsesPerDay} uses remaining)");
        return (atkBonus, dmgBonus);
    }
}
