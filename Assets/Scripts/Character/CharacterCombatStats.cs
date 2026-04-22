using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encapsulates combat-stat and attack-sequence math used by <see cref="CharacterController"/>.
/// </summary>
public class CharacterCombatStats : MonoBehaviour
{
    private CharacterController _character;

    public void Initialize(CharacterController character)
    {
        _character = character;
    }

    /// <summary>
    /// Number of iterative main-hand attacks granted by BAB.
    /// </summary>
    public int GetNumberOfAttacks()
    {
        int bab = _character != null && _character.Stats != null ? _character.Stats.BaseAttackBonus : 0;
        if (bab >= 16) return 4;
        if (bab >= 11) return 3;
        if (bab >= 6) return 2;
        return 1;
    }

    /// <summary>
    /// Iterative attack BAB values (BAB, BAB-5, BAB-10, BAB-15).
    /// </summary>
    public List<int> GetAttackBonuses()
    {
        int bab = _character != null && _character.Stats != null ? _character.Stats.BaseAttackBonus : 0;
        var bonuses = new List<int> { bab };

        if (bab >= 6) bonuses.Add(bab - 5);
        if (bab >= 11) bonuses.Add(bab - 10);
        if (bab >= 16) bonuses.Add(bab - 15);

        return bonuses;
    }

    public int GetIterativeAttackCount()
    {
        return GetAttackBonuses().Count;
    }

    public int GetIterativeAttackBAB(int attackIndex)
    {
        List<int> bonuses = GetAttackBonuses();
        if (attackIndex < 0 || attackIndex >= bonuses.Count)
            return 0;

        return bonuses[attackIndex];
    }

    /// <summary>
    /// Off-hand iterative count for two-weapon fighting.
    /// </summary>
    public int GetOffHandAttackCount()
    {
        if (_character == null || _character.Stats == null || !_character.Stats.HasFeat("Two-Weapon Fighting"))
            return 0;

        if (!_character.CanDualWield())
            return 0;

        int count = 1;
        int bab = _character.Stats.BaseAttackBonus;

        if (_character.Stats.HasFeat("Improved Two-Weapon Fighting") && bab >= 6)
            count++;

        if (_character.Stats.HasFeat("Greater Two-Weapon Fighting") && bab >= 11)
            count++;

        return count;
    }

    public int GetOffHandAttackBAB(int attackIndex)
    {
        int bab = _character != null && _character.Stats != null ? _character.Stats.BaseAttackBonus : 0;
        if (attackIndex < 0)
            attackIndex = 0;

        return bab - (attackIndex * 5);
    }

    public int GetGrappleSizeModifier()
    {
        return _character != null && _character.Stats != null
            ? _character.Stats.CurrentSizeCategory.GetGrappleModifier()
            : 0;
    }

    public int GetGrappleModifier(int? baseAttackBonusOverride = null)
    {
        if (_character == null || _character.Stats == null)
            return 0;

        int sizeMod = _character.Stats.CurrentSizeCategory.GetGrappleModifier();
        int bab = baseAttackBonusOverride ?? _character.Stats.BaseAttackBonus;
        return bab
            + _character.Stats.STRMod
            + sizeMod
            + _character.Stats.ConditionAttackPenalty
            + (_character.Stats.HasFeat("Improved Grapple") ? 4 : 0);
    }
}
