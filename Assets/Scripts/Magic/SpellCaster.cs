using UnityEngine;

/// <summary>
/// Static utility class that resolves spell casting — performs rolls, applies damage/healing/buffs.
/// Analogous to CombatUtils for melee/ranged attacks.
///
/// Two entry points:
/// - Cast(SpellData, CharacterStats, CharacterStats): Used by GameManager when slots are consumed externally.
/// - CastSpell(CharacterController, CharacterController, SpellData): Full pipeline including slot consumption.
/// </summary>
public static class SpellCaster
{
    /// <summary>
    /// Simplified spell resolution using stats only.
    /// Slot consumption is handled by the caller (GameManager).
    /// Mage Armor AC application is also handled externally.
    /// </summary>
    public static SpellResult Cast(SpellData spell, CharacterStats casterStats, CharacterStats targetStats)
    {
        var result = new SpellResult();
        result.Spell = spell;
        result.CasterName = casterStats.CharacterName;
        result.TargetName = targetStats.CharacterName;
        result.DamageType = spell.DamageType ?? "";
        result.Success = true;

        // ========== ATTACK ROLL (touch attacks for damage spells) ==========
        if (spell.EffectType == SpellEffectType.Damage && !spell.AutoHit)
        {
            result.RequiredAttackRoll = true;
            bool isRanged = spell.RangeSquares > 1;
            result.IsRangedTouch = isRanged;

            int atkBonus = isRanged
                ? casterStats.BaseAttackBonus + casterStats.DEXMod + casterStats.SizeModifier
                : casterStats.BaseAttackBonus + casterStats.STRMod + casterStats.SizeModifier;
            int touchAC = SpellcastingComponent.GetTouchAC(targetStats);

            int roll = Random.Range(1, 21);
            int total = roll + atkBonus;

            result.AttackRoll = roll;
            result.AttackBonus = atkBonus;
            result.AttackTotal = total;
            result.TouchAC = touchAC;

            if (roll == 20)
                result.AttackHit = true;
            else if (roll == 1)
                result.AttackHit = false;
            else
                result.AttackHit = total >= touchAC;

            if (!result.AttackHit)
            {
                result.Success = false;
                return result;
            }
        }
        else if (spell.AutoHit)
        {
            result.RequiredAttackRoll = false;
            result.AttackHit = true;
        }
        else
        {
            result.AttackHit = true;
        }

        // ========== SAVING THROW ==========
        if (spell.AllowsSavingThrow && result.AttackHit)
        {
            result.RequiredSave = true;
            result.SaveType = spell.SavingThrowType;
            int castingMod = casterStats.IsWizard ? casterStats.INTMod : casterStats.WISMod;
            result.SaveDC = 10 + spell.SpellLevel + castingMod;

            int saveRoll = Random.Range(1, 21);
            int saveMod = GetSaveModifier(targetStats, spell.SavingThrowType);
            result.SaveRoll = saveRoll;
            result.SaveMod = saveMod;
            result.SaveTotal = saveRoll + saveMod;
            result.SaveSucceeded = result.SaveTotal >= result.SaveDC;
        }

        // ========== DAMAGE ==========
        if (spell.EffectType == SpellEffectType.Damage && result.AttackHit)
        {
            result.TargetHPBefore = targetStats.CurrentHP;

            if (spell.AutoHit && spell.MissileCount > 0)
            {
                // Magic Missile: 1 + (CL-1)/2 missiles, max 5
                int missileCount = Mathf.Min(5, 1 + (casterStats.Level - 1) / 2);
                result.MissileCount = missileCount;
                result.MissileDamages = new int[missileCount];
                int totalDmg = 0;

                for (int i = 0; i < missileCount; i++)
                {
                    int missileDmg = Random.Range(1, spell.DamageDice + 1) + spell.BonusDamage;
                    result.MissileDamages[i] = missileDmg;
                    totalDmg += missileDmg;
                }
                result.DamageRolled = totalDmg;
                result.DamageDealt = totalDmg;
            }
            else
            {
                int dmg = 0;
                for (int i = 0; i < spell.DamageCount; i++)
                    dmg += Random.Range(1, spell.DamageDice + 1);
                dmg += spell.BonusDamage;
                result.DamageRolled = Mathf.Max(0, dmg);

                if (result.RequiredSave && result.SaveSucceeded && spell.SaveHalves)
                    result.DamageDealt = Mathf.Max(0, result.DamageRolled / 2);
                else if (result.RequiredSave && result.SaveSucceeded && !spell.SaveHalves)
                    result.DamageDealt = 0;
                else
                    result.DamageDealt = Mathf.Max(0, result.DamageRolled);
            }

            if (result.DamageDealt > 0)
                result.DamageDealt = Mathf.Max(1, result.DamageDealt);

            targetStats.TakeDamage(result.DamageDealt);
            result.TargetHPAfter = targetStats.CurrentHP;
            result.TargetKilled = targetStats.IsDead;
        }

        // ========== HEALING ==========
        if (spell.EffectType == SpellEffectType.Healing)
        {
            result.TargetHPBefore = targetStats.CurrentHP;

            int healRoll = 0;
            for (int i = 0; i < spell.HealCount; i++)
                healRoll += Random.Range(1, spell.HealDice + 1);
            result.HealRolled = healRoll;

            int totalHeal = healRoll + spell.BonusHealing;
            totalHeal = Mathf.Max(1, totalHeal);

            targetStats.CurrentHP = Mathf.Min(targetStats.CurrentHP + totalHeal, targetStats.TotalMaxHP);
            result.HealingDone = totalHeal;
            result.TargetHPAfter = targetStats.CurrentHP;
        }

        // ========== BUFF ==========
        if (spell.EffectType == SpellEffectType.Buff)
        {
            result.BuffApplied = true;
            if (spell.SpellId == "mage_armor")
                result.BuffDescription = $"+{spell.BuffACBonus} armor AC bonus (Mage Armor)";
            else
                result.BuffDescription = spell.Description;
        }

        return result;
    }

    /// <summary>
    /// Get the appropriate saving throw modifier for a character.
    /// </summary>
    private static int GetSaveModifier(CharacterStats stats, string saveType)
    {
        switch (saveType)
        {
            case "Reflex": return stats.ReflexSave;
            case "Will": return stats.WillSave;
            case "Fortitude": return stats.FortitudeSave;
            default: return 0;
        }
    }
}
