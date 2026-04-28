using System;
using UnityEngine;

/// <summary>
/// Tracks D&D 3.5e ability score damage (temporary) and drain (persistent).
/// Effective score calculations should subtract both damage and drain.
/// </summary>
[Serializable]
public class AbilityScoreDamage
{
    public int StrengthDamage;
    public int DexterityDamage;
    public int ConstitutionDamage;
    public int IntelligenceDamage;
    public int WisdomDamage;
    public int CharismaDamage;

    public int StrengthDrain;
    public int DexterityDrain;
    public int ConstitutionDrain;
    public int IntelligenceDrain;
    public int WisdomDrain;
    public int CharismaDrain;

    public int GetDamage(AbilityType ability)
    {
        switch (ability)
        {
            case AbilityType.STR: return StrengthDamage;
            case AbilityType.DEX: return DexterityDamage;
            case AbilityType.CON: return ConstitutionDamage;
            case AbilityType.INT: return IntelligenceDamage;
            case AbilityType.WIS: return WisdomDamage;
            case AbilityType.CHA: return CharismaDamage;
            default: return 0;
        }
    }

    public int GetDrain(AbilityType ability)
    {
        switch (ability)
        {
            case AbilityType.STR: return StrengthDrain;
            case AbilityType.DEX: return DexterityDrain;
            case AbilityType.CON: return ConstitutionDrain;
            case AbilityType.INT: return IntelligenceDrain;
            case AbilityType.WIS: return WisdomDrain;
            case AbilityType.CHA: return CharismaDrain;
            default: return 0;
        }
    }

    public int GetTotalPenalty(AbilityType ability) => GetDamage(ability) + GetDrain(ability);

    public int ApplyDamage(AbilityType ability, int amount)
    {
        if (amount <= 0)
            return 0;

        switch (ability)
        {
            case AbilityType.STR: StrengthDamage += amount; break;
            case AbilityType.DEX: DexterityDamage += amount; break;
            case AbilityType.CON: ConstitutionDamage += amount; break;
            case AbilityType.INT: IntelligenceDamage += amount; break;
            case AbilityType.WIS: WisdomDamage += amount; break;
            case AbilityType.CHA: CharismaDamage += amount; break;
            default: return 0;
        }

        return amount;
    }

    public int ApplyDrain(AbilityType ability, int amount)
    {
        if (amount <= 0)
            return 0;

        switch (ability)
        {
            case AbilityType.STR: StrengthDrain += amount; break;
            case AbilityType.DEX: DexterityDrain += amount; break;
            case AbilityType.CON: ConstitutionDrain += amount; break;
            case AbilityType.INT: IntelligenceDrain += amount; break;
            case AbilityType.WIS: WisdomDrain += amount; break;
            case AbilityType.CHA: CharismaDrain += amount; break;
            default: return 0;
        }

        return amount;
    }

    public int HealDamage(AbilityType ability, int amount)
    {
        if (amount <= 0)
            return 0;

        int current = GetDamage(ability);
        int healed = Mathf.Min(amount, current);

        switch (ability)
        {
            case AbilityType.STR: StrengthDamage = Mathf.Max(0, StrengthDamage - healed); break;
            case AbilityType.DEX: DexterityDamage = Mathf.Max(0, DexterityDamage - healed); break;
            case AbilityType.CON: ConstitutionDamage = Mathf.Max(0, ConstitutionDamage - healed); break;
            case AbilityType.INT: IntelligenceDamage = Mathf.Max(0, IntelligenceDamage - healed); break;
            case AbilityType.WIS: WisdomDamage = Mathf.Max(0, WisdomDamage - healed); break;
            case AbilityType.CHA: CharismaDamage = Mathf.Max(0, CharismaDamage - healed); break;
            default: return 0;
        }

        return healed;
    }

    public int RemoveDrain(AbilityType ability, int amount)
    {
        if (amount <= 0)
            return 0;

        int current = GetDrain(ability);
        int removed = Mathf.Min(amount, current);

        switch (ability)
        {
            case AbilityType.STR: StrengthDrain = Mathf.Max(0, StrengthDrain - removed); break;
            case AbilityType.DEX: DexterityDrain = Mathf.Max(0, DexterityDrain - removed); break;
            case AbilityType.CON: ConstitutionDrain = Mathf.Max(0, ConstitutionDrain - removed); break;
            case AbilityType.INT: IntelligenceDrain = Mathf.Max(0, IntelligenceDrain - removed); break;
            case AbilityType.WIS: WisdomDrain = Mathf.Max(0, WisdomDrain - removed); break;
            case AbilityType.CHA: CharismaDrain = Mathf.Max(0, CharismaDrain - removed); break;
            default: return 0;
        }

        return removed;
    }

    public bool HasAnyDamageOrDrain()
    {
        return StrengthDamage > 0 || DexterityDamage > 0 || ConstitutionDamage > 0 ||
               IntelligenceDamage > 0 || WisdomDamage > 0 || CharismaDamage > 0 ||
               StrengthDrain > 0 || DexterityDrain > 0 || ConstitutionDrain > 0 ||
               IntelligenceDrain > 0 || WisdomDrain > 0 || CharismaDrain > 0;
    }
}
