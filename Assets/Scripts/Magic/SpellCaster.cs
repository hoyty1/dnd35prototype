using UnityEngine;

/// <summary>
/// Static utility class that resolves spell casting — performs rolls, applies damage/healing/buffs.
/// Analogous to CombatUtils for melee/ranged attacks.
///
/// Two entry points:
/// - Cast(SpellData, CharacterStats, CharacterStats): Used by GameManager when slots are consumed externally.
/// - Cast(SpellData, CharacterStats, CharacterStats, MetamagicData): With metamagic effects applied.
/// </summary>
public static class SpellCaster
{
    /// <summary>
    /// Simplified spell resolution using stats only (no metamagic).
    /// Slot consumption is handled by the caller (GameManager).
    /// Mage Armor AC application is also handled externally.
    /// </summary>
    public static SpellResult Cast(SpellData spell, CharacterStats casterStats, CharacterStats targetStats)
    {
        return Cast(spell, casterStats, targetStats, null);
    }

    /// <summary>
    /// Spell resolution with metamagic support.
    /// Slot consumption is handled by the caller (GameManager).
    /// Mage Armor AC application is also handled externally.
    /// </summary>
    public static SpellResult Cast(SpellData spell, CharacterStats casterStats, CharacterStats targetStats, MetamagicData metamagic)
    {
        var result = new SpellResult();
        result.Spell = spell;
        result.CasterName = casterStats.CharacterName;
        result.TargetName = targetStats.CharacterName;
        result.DamageType = spell.DamageType ?? "";
        result.Success = true;
        result.Metamagic = metamagic;

        bool isEmpowered = metamagic != null && metamagic.Has(MetamagicFeatId.EmpowerSpell);
        bool isMaximized = metamagic != null && metamagic.Has(MetamagicFeatId.MaximizeSpell);
        bool isHeightened = metamagic != null && metamagic.Has(MetamagicFeatId.HeightenSpell);

        // ========== DETERMINE EFFECTIVE SPELL LEVEL (for save DC with Heighten) ==========
        int effectiveSpellLevel = spell.SpellLevel;
        if (isHeightened && metamagic.HeightenToLevel > spell.SpellLevel)
        {
            effectiveSpellLevel = metamagic.HeightenToLevel;
        }

        // ========== ATTACK ROLL (touch attacks for single-target damage spells) ==========
        // AoE spells with saving throws (Burning Hands, Fireball, etc.) do NOT require attack rolls.
        // They auto-hit all targets in the area; targets then make saving throws.
        // Only single-target touch spells (Shocking Grasp, Scorching Ray) need attack rolls.
        bool isAoESpell = spell.TargetType == SpellTargetType.Area;
        if (spell.EffectType == SpellEffectType.Damage && !spell.AutoHit && !isAoESpell)
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
            // Heighten Spell increases save DC by using the heightened spell level
            result.SaveDC = 10 + effectiveSpellLevel + castingMod;

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
                    int missileDmg;
                    if (isMaximized)
                        missileDmg = spell.DamageDice + spell.BonusDamage; // Max die value
                    else
                        missileDmg = Random.Range(1, spell.DamageDice + 1) + spell.BonusDamage;

                    result.MissileDamages[i] = missileDmg;
                    totalDmg += missileDmg;
                }

                // Empower: multiply total by 1.5
                if (isEmpowered)
                {
                    int empowerBonus = Mathf.RoundToInt(totalDmg * 0.5f);
                    result.EmpowerBonus = empowerBonus;
                    totalDmg += empowerBonus;
                }

                result.DamageRolled = totalDmg;
                result.DamageDealt = totalDmg;
            }
            else
            {
                int dmg = 0;
                for (int i = 0; i < spell.DamageCount; i++)
                {
                    if (isMaximized)
                        dmg += spell.DamageDice; // Max die value
                    else
                        dmg += Random.Range(1, spell.DamageDice + 1);
                }
                dmg += spell.BonusDamage;
                int baseDmg = Mathf.Max(0, dmg);

                // Empower: multiply variable portion by 1.5
                if (isEmpowered)
                {
                    int empowerBonus = Mathf.RoundToInt(baseDmg * 0.5f);
                    result.EmpowerBonus = empowerBonus;
                    baseDmg += empowerBonus;
                }

                result.DamageRolled = baseDmg;

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
            {
                if (isMaximized)
                    healRoll += spell.HealDice; // Max die value
                else
                    healRoll += Random.Range(1, spell.HealDice + 1);
            }
            result.HealRolled = healRoll;

            int totalHeal = healRoll + spell.BonusHealing;

            // Empower: multiply total by 1.5
            if (isEmpowered)
            {
                int empowerBonus = Mathf.RoundToInt(totalHeal * 0.5f);
                result.EmpowerBonus = empowerBonus;
                totalHeal += empowerBonus;
            }

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
    /// Apply metamagic modifications to a cloned SpellData before casting.
    /// Call this on a Clone() of the spell - do NOT modify the original.
    /// Handles: Enlarge (double range), Extend (double duration), Widen (double area),
    /// Quicken (change action type).
    /// Empower/Maximize/Heighten are handled during Cast() resolution.
    /// </summary>
    public static void ApplyMetamagicToSpellData(SpellData spell, MetamagicData metamagic)
    {
        if (metamagic == null || !metamagic.HasAnyMetamagic) return;

        // Enlarge Spell: double range
        if (metamagic.Has(MetamagicFeatId.EnlargeSpell) && spell.RangeSquares > 0)
        {
            spell.RangeSquares *= 2;
            Debug.Log($"[Metamagic] Enlarge Spell: range doubled to {spell.RangeSquares} squares");
        }

        // Extend Spell: double duration
        if (metamagic.Has(MetamagicFeatId.ExtendSpell) && spell.BuffDurationRounds != 0)
        {
            if (spell.BuffDurationRounds > 0)
                spell.BuffDurationRounds *= 2;
            // -1 (hours/level) stays as -1 but could be tracked separately
            Debug.Log($"[Metamagic] Extend Spell: duration doubled to {spell.BuffDurationRounds} rounds");
        }

        // Widen Spell: double area radius
        if (metamagic.Has(MetamagicFeatId.WidenSpell) && spell.AreaRadius > 0)
        {
            spell.AreaRadius *= 2;
            Debug.Log($"[Metamagic] Widen Spell: area doubled to {spell.AreaRadius} sq radius");
        }

        // Quicken Spell: change action type to free
        if (metamagic.Has(MetamagicFeatId.QuickenSpell))
        {
            spell.ActionType = SpellActionType.Free;
            Debug.Log($"[Metamagic] Quicken Spell: action type changed to Free");
        }
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
