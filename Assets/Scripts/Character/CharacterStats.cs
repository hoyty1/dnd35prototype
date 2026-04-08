using UnityEngine;

/// <summary>
/// Holds RPG stats for a character.
/// </summary>
[System.Serializable]
public class CharacterStats
{
    public string CharacterName;
    public int MaxHP;
    public int CurrentHP;
    public int ArmorClass;     // Target number to hit (d20 roll >= AC = hit)
    public int AttackBonus;    // Added to d20 roll
    public int MinDamage;
    public int MaxDamage;
    public int MoveRange;      // Hex tiles per turn
    public int AttackRange;    // Hex tiles for attack reach

    public bool IsDead => CurrentHP <= 0;

    public CharacterStats(string name, int maxHP, int ac, int atkBonus, int minDmg, int maxDmg, int moveRange, int atkRange)
    {
        CharacterName = name;
        MaxHP = maxHP;
        CurrentHP = maxHP;
        ArmorClass = ac;
        AttackBonus = atkBonus;
        MinDamage = minDmg;
        MaxDamage = maxDmg;
        MoveRange = moveRange;
        AttackRange = atkRange;
    }

    /// <summary>
    /// Roll a d20 + attack bonus against target AC.
    /// Returns (hit, rollValue, totalRoll).
    /// </summary>
    public (bool hit, int roll, int total) RollToHit(int targetAC)
    {
        int roll = Random.Range(1, 21); // 1-20
        int total = roll + AttackBonus;
        bool hit = total >= targetAC;
        return (hit, roll, total);
    }

    /// <summary>
    /// Roll damage.
    /// </summary>
    public int RollDamage()
    {
        return Random.Range(MinDamage, MaxDamage + 1);
    }

    /// <summary>
    /// Apply damage, clamping HP to 0.
    /// </summary>
    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }
}
