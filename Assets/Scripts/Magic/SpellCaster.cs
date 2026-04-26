using System.Collections.Generic;
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
        return Cast(spell, casterStats, targetStats, null, false, false, null, null);
    }

    /// <summary>
    /// Spell resolution with metamagic support.
    /// Slot consumption is handled by the caller (GameManager).
    /// Mage Armor AC application is also handled externally.
    /// </summary>
    public static SpellResult Cast(SpellData spell, CharacterStats casterStats, CharacterStats targetStats, MetamagicData metamagic, bool forceFriendlyTouchNoRoll = false, bool forceTargetToFailSave = false)
    {
        return Cast(spell, casterStats, targetStats, metamagic, forceFriendlyTouchNoRoll, forceTargetToFailSave, null, null);
    }

    /// <summary>
    /// Spell resolution with optional controller context for situational modifiers
    /// (fighting defensively, shooting into melee penalties, etc.).
    /// </summary>
    public static SpellResult Cast(
        SpellData spell,
        CharacterStats casterStats,
        CharacterStats targetStats,
        MetamagicData metamagic,
        bool forceFriendlyTouchNoRoll,
        bool forceTargetToFailSave,
        CharacterController casterController,
        CharacterController targetController)
    {
        var result = new SpellResult();
        result.Spell = spell;
        result.CasterName = casterStats.CharacterName;
        result.TargetName = targetStats.CharacterName;
        result.DamageType = spell.DamageType ?? "";

        var parsedSpellDamageTypes = DamageTextUtils.ParseDamageTypes(spell.DamageType);
        result.DamageTypeSummary = DamageTextUtils.FormatDamageTypes(parsedSpellDamageTypes);
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

        // ========== ATTACK ROLL (touch attacks) ==========
        // AoE spells do not use touch attack rolls.
        bool isAoESpell = spell.TargetType == SpellTargetType.Area;
        bool usesTouchAttack = spell.IsMeleeTouchSpell() || spell.IsRangedTouchSpell();

        if (usesTouchAttack && forceFriendlyTouchNoRoll)
        {
            // Friendly touch delivery (heals/buffs) should not require attack roll.
            result.RequiredAttackRoll = false;
            result.AttackHit = true;
            result.IsRangedTouch = false;
        }
        else if (usesTouchAttack && !spell.AutoHit && !isAoESpell)
        {
            result.RequiredAttackRoll = true;
            bool isRanged = spell.IsRangedTouchSpell();
            result.IsRangedTouch = isRanged;

            int atkBonus = isRanged
                ? casterStats.BaseAttackBonus + casterStats.DEXMod + casterStats.SizeModifier
                : casterStats.BaseAttackBonus + casterStats.STRMod + casterStats.SizeModifier;

            int fightingDefensivelyPenalty = (casterController != null && casterController.IsFightingDefensively) ? -4 : 0;
            bool preciseShotNegated = false;
            int shootingIntoMeleePenalty = 0;
            if (isRanged)
            {
                shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(casterController, targetController, out preciseShotNegated);
            }

            int touchAC = SpellcastingComponent.GetTouchAC(targetStats)
                + ((targetController != null && targetController.IsFightingDefensively) ? 2 : 0);

            int roll = Random.Range(1, 21);
            int total = roll + atkBonus + fightingDefensivelyPenalty + shootingIntoMeleePenalty;

            result.AttackRoll = roll;
            result.AttackBonus = atkBonus;
            result.AttackTotal = total;
            result.TouchAC = touchAC;
            result.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            result.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            result.PreciseShotNegated = preciseShotNegated;
            result.TargetFightingDefensivelyACBonus = (targetController != null && targetController.IsFightingDefensively) ? 2 : 0;

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

        // ========== MIND-AFFECTING IMMUNITY ==========
        if (result.AttackHit && spell.IsMindAffecting && IsImmuneToMindAffecting(targetStats))
        {
            result.MindAffectingImmunityBlocked = true;
            result.Success = false;
            return result;
        }

        // ========== SPELL RESISTANCE ==========
        if (result.AttackHit && spell.SpellResistanceApplies && targetStats != null && targetStats.SpellResistance > 0)
        {
            result.SpellResistanceChecked = true;
            result.SpellResistanceValue = targetStats.SpellResistance;
            result.SpellResistanceRoll = Random.Range(1, 21);
            result.SpellResistanceTotal = result.SpellResistanceRoll + GetCasterLevelForSpellResistanceCheck(casterStats);
            result.SpellResistancePassed = result.SpellResistanceTotal >= result.SpellResistanceValue;

            if (!result.SpellResistancePassed)
            {
                result.Success = false;
                return result;
            }
        }

        // ========== SHIELD VS MAGIC MISSILE ==========
        if (result.AttackHit && IsMagicMissileSpell(spell) && HasActiveShieldSpell(targetController))
        {
            result.MagicMissileBlockedByShield = true;
            result.DamageRolled = 0;
            result.DamageDealt = 0;
            result.TargetHPBefore = targetStats.CurrentHP;
            result.TargetHPAfter = targetStats.CurrentHP;
            return result;
        }

        // ========== SAVING THROW ==========
        if (spell.AllowsSavingThrow && result.AttackHit)
        {
            result.RequiredSave = true;
            result.SaveType = spell.SavingThrowType;
            int castingMod = casterStats.IsWizard ? casterStats.INTMod : casterStats.WISMod;
            // Heighten Spell increases save DC by using the heightened spell level
            result.SaveDC = 10 + effectiveSpellLevel + castingMod;

            if (forceTargetToFailSave)
            {
                result.SaveRoll = 1;
                result.SaveMod = 0;
                result.SaveTotal = 1;
                result.SaveSucceeded = false;
            }
            else
            {
                int saveRoll = Random.Range(1, 21);
                int saveMod = GetSaveModifier(targetStats, spell);
                result.SaveRoll = saveRoll;
                result.SaveMod = saveMod;
                result.SaveTotal = saveRoll + saveMod;
                result.SaveSucceeded = result.SaveTotal >= result.SaveDC;
            }
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

            int effectiveRangeSquares = spell.GetRangeSquaresForCasterLevel(casterStats != null ? casterStats.Level : 0);
            bool isRangedSpellDamage = result.RequiredAttackRoll
                ? result.IsRangedTouch
                : (effectiveRangeSquares > 1 || spell.TargetType == SpellTargetType.Area);

            var packet = new DamagePacket
            {
                RawDamage = result.DamageDealt,
                Types = parsedSpellDamageTypes,
                AttackTags = DamageBypassTag.None,
                IsRanged = isRangedSpellDamage,
                IsNonlethal = false,
                Source = AttackSource.Spell,
                SourceName = spell.Name
            };

            DamageResolutionResult mitigation = targetStats.ApplyIncomingDamage(result.DamageDealt, packet);
            result.ResistancePrevented = mitigation.ResistanceApplied;
            result.DRPrevented = mitigation.DamageReductionApplied;
            result.ImmunityPrevented = mitigation.ImmunityTriggered;
            result.MitigationSummary = mitigation.GetMitigationSummary();
            result.DamageDealt = mitigation.FinalDamage;

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

            int nonlethalHealed;
            result.HealingDone = targetStats.HealDamage(totalHeal, out nonlethalHealed);
            result.NonlethalHealed = nonlethalHealed;
            result.TargetHPAfter = targetStats.CurrentHP;
        }

        // ========== BUFF / DEBUFF ==========
        if (spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Debuff)
        {
            bool debuffNegatedBySave = spell.EffectType == SpellEffectType.Debuff && result.RequiredSave && result.SaveSucceeded;
            result.BuffApplied = !debuffNegatedBySave;

            if (spell.SpellId == "mage_armor")
                result.BuffDescription = $"+{spell.BuffACBonus} armor AC bonus (Mage Armor)";
            else if (spell.EffectType == SpellEffectType.Debuff)
                result.BuffDescription = debuffNegatedBySave
                    ? $"Debuff negated by save: {spell.Description}"
                    : $"Debuff: {spell.Description}";
            else
                result.BuffDescription = spell.Description;
        }

        return result;
    }

    private static bool IsMagicMissileSpell(SpellData spell)
    {
        return spell != null && spell.SpellId == "magic_missile";
    }

    private static bool HasActiveShieldSpell(CharacterController targetController)
    {
        if (targetController == null)
            return false;

        StatusEffectManager statusMgr = targetController.GetComponent<StatusEffectManager>();
        if (statusMgr != null && statusMgr.ActiveEffects != null)
        {
            for (int i = 0; i < statusMgr.ActiveEffects.Count; i++)
            {
                ActiveSpellEffect effect = statusMgr.ActiveEffects[i];
                if (effect?.Spell == null)
                    continue;

                if (effect.Spell.SpellId == "shield" && effect.RemainingRounds > 0)
                    return true;
            }
        }

        SpellcastingComponent spellComp = targetController.GetComponent<SpellcastingComponent>();
        if (spellComp != null
            && spellComp.ActiveBuffs != null
            && spellComp.ActiveBuffs.TryGetValue("shield", out int rounds)
            && rounds > 0)
        {
            return true;
        }

        return false;
    }

    private static List<CharacterController> GetAllCombatCharactersSnapshot()
    {
        var allChars = new List<CharacterController>();
        var gm = GameManager.Instance;
        if (gm == null) return allChars;

        if (gm.PCs != null)
        {
            for (int i = 0; i < gm.PCs.Count; i++)
            {
                var pc = gm.PCs[i];
                if (pc != null) allChars.Add(pc);
            }
        }

        if (gm.NPCs != null)
        {
            for (int i = 0; i < gm.NPCs.Count; i++)
            {
                var npc = gm.NPCs[i];
                if (npc != null) allChars.Add(npc);
            }
        }

        return allChars;
    }

    private static int GetShootingIntoMeleePenalty(CharacterController caster, CharacterController target, out bool preciseShotNegated)
    {
        preciseShotNegated = false;
        if (caster == null || target == null || caster.Stats == null || target.Stats == null) return 0;

        List<CharacterController> allChars = GetAllCombatCharactersSnapshot();
        if (allChars.Count == 0) return 0;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(target.GridPosition, target, allChars);
        bool engagedWithCasterAlly = false;
        for (int i = 0; i < threateningEnemies.Count; i++)
        {
            var threatener = threateningEnemies[i];
            if (threatener == null || threatener == caster) continue;
            if (threatener.Stats == null || threatener.Stats.IsDead) continue;

            if (threatener.Team == caster.Team)
            {
                engagedWithCasterAlly = true;
                break;
            }
        }

        if (!engagedWithCasterAlly) return 0;

        if (caster.Stats.HasFeat("Precise Shot"))
        {
            preciseShotNegated = true;
            return 0;
        }

        return -4;
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
            if (spell.RangeIncreaseSquares > 0)
                spell.RangeIncreaseSquares *= 2;
            Debug.Log($"[Metamagic] Enlarge Spell: range doubled to {spell.RangeSquares} squares (scaling +{spell.RangeIncreaseSquares}/{spell.RangeIncreasePerLevels} lv)");
        }

        // Extend Spell: double duration
        if (metamagic.Has(MetamagicFeatId.ExtendSpell))
        {
            // New duration system: double the DurationValue
            if (spell.DurationValue > 0)
            {
                spell.DurationValue *= 2;
                Debug.Log($"[Metamagic] Extend Spell: duration value doubled to {spell.DurationValue}");
            }
            // Legacy: also double BuffDurationRounds
            if (spell.BuffDurationRounds > 0)
            {
                spell.BuffDurationRounds *= 2;
                Debug.Log($"[Metamagic] Extend Spell: legacy duration doubled to {spell.BuffDurationRounds} rounds");
            }
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

        // Silent Spell: remove verbal component requirement
        if (metamagic.Has(MetamagicFeatId.SilentSpell))
        {
            spell.HasVerbalComponent = false;
            Debug.Log("[Metamagic] Silent Spell: verbal component removed");
        }

        // Still Spell: remove somatic component requirement
        if (metamagic.Has(MetamagicFeatId.StillSpell))
        {
            spell.HasSomaticComponent = false;
            Debug.Log("[Metamagic] Still Spell: somatic component removed");
        }
    }

    private static int GetCasterLevelForSpellResistanceCheck(CharacterStats casterStats)
    {
        if (casterStats == null)
            return 0;

        int casterLevel = casterStats.GetCasterLevel();
        if (casterLevel > 0)
            return casterLevel;

        return Mathf.Max(1, casterStats.Level);
    }

    private static bool IsImmuneToMindAffecting(CharacterStats targetStats)
    {
        if (targetStats == null)
            return false;

        string creatureType = string.IsNullOrWhiteSpace(targetStats.CreatureType)
            ? string.Empty
            : targetStats.CreatureType.Trim().ToLowerInvariant();

        if (creatureType == "undead" || creatureType == "construct" || creatureType == "ooze" || creatureType == "plant" || creatureType == "vermin")
            return true;

        if (targetStats.SpecialAbilities != null)
        {
            for (int i = 0; i < targetStats.SpecialAbilities.Count; i++)
            {
                string trait = targetStats.SpecialAbilities[i];
                if (string.IsNullOrWhiteSpace(trait))
                    continue;

                string normalized = trait.ToLowerInvariant();
                if (normalized.Contains("mind-affect") || normalized.Contains("mind affecting") || normalized.Contains("mindless"))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get the appropriate saving throw modifier for a character,
    /// including conditional bonuses such as Still Mind (+2 vs enchantment effects).
    /// </summary>
    private static int GetSaveModifier(CharacterStats stats, SpellData spell)
    {
        if (stats == null || spell == null)
            return 0;

        int baseSave;
        switch (spell.SavingThrowType)
        {
            case "Reflex":
                baseSave = stats.ReflexSave;
                break;
            case "Will":
                baseSave = stats.WillSave;
                break;
            case "Fortitude":
                baseSave = stats.FortitudeSave;
                break;
            default:
                baseSave = 0;
                break;
        }

        bool isEnchantment = !string.IsNullOrWhiteSpace(spell.School)
            && spell.School.Trim().Equals("Enchantment", System.StringComparison.OrdinalIgnoreCase);

        if (spell.SavingThrowType == "Will" && isEnchantment && stats.StillMindBonus > 0)
            baseSave += stats.StillMindBonus;

        return baseSave;
    }
}