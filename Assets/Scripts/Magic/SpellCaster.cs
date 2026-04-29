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

        Alignment sourceAlignment = casterStats != null ? casterStats.CharacterAlignment : Alignment.None;
        AlignmentProtectionBenefits protection = AlignmentProtectionRules.GetBenefitsAgainst(targetController, sourceAlignment);
        result.ProtectionSourceName = GetActiveAlignmentProtectionSourceName(targetController);
        if (!string.IsNullOrEmpty(protection.SourceSpellName))
            result.ProtectionSourceName = protection.SourceSpellName;

        // ========== DETERMINE EFFECTIVE SPELL LEVEL (for save DC with Heighten) ==========
        int effectiveSpellLevel = spell.SpellLevel;
        if (isHeightened && metamagic.HeightenToLevel > spell.SpellLevel)
        {
            effectiveSpellLevel = metamagic.HeightenToLevel;
        }

        // ========== DEAFENED VERBAL SPELL FAILURE ==========
        if (casterStats != null
            && spell != null
            && spell.HasVerbalComponent
            && HasCondition(casterStats, CombatConditionType.Deafened))
        {
            int failureRoll = Random.Range(1, 101);
            result.SaveRoll = 0;
            if (failureRoll <= 20)
            {
                result.Success = false;
                result.TargetHPBefore = targetStats != null ? targetStats.CurrentHP : 0;
                result.TargetHPAfter = result.TargetHPBefore;
                result.NoEffectReason = $"Spell fails due to deafness (20% verbal component failure, d%={failureRoll:00}).";
                return result;
            }
        }

        // ========== ATTACK ROLL (touch attacks) ==========
        // AoE spells do not use touch attack rolls.
        bool isAoESpell = spell.TargetType == SpellTargetType.Area;
        bool usesTouchAttack = spell.IsMeleeTouchSpell() || spell.IsRangedTouchSpell();

        bool casterIsSummoned = casterController != null
            && GameManager.Instance != null
            && GameManager.Instance.IsSummonedCreature(casterController);

        if (usesTouchAttack && spell.IsMeleeTouchSpell() && casterIsSummoned && protection.HasMatch && protection.BlocksSummonedContact)
        {
            result.AttackHit = false;
            result.Success = false;
            result.TargetHPBefore = targetStats != null ? targetStats.CurrentHP : 0;
            result.TargetHPAfter = result.TargetHPBefore;
            result.SummonedContactBlockedByProtection = true;
            result.NoEffectReason = "Protection from alignment barrier blocks bodily contact by this summoned creature.";
            return result;
        }

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

            int situationalSpellAttackBonus = 0;
            string situationalSpellAttackSource = string.Empty;
            if (ShouldApplyShockingGraspMetalBonus(spell, targetController, targetStats, out string shockingGraspBonusSource))
            {
                situationalSpellAttackBonus += 3;
                situationalSpellAttackSource = shockingGraspBonusSource;
            }

            int fightingDefensivelyPenalty = (casterController != null && casterController.IsFightingDefensively) ? -4 : 0;
            bool preciseShotNegated = false;
            int shootingIntoMeleePenalty = 0;
            if (isRanged)
            {
                shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(casterController, targetController, out preciseShotNegated);
            }

            int touchAC = SpellcastingComponent.GetTouchAC(targetStats)
                + ((targetController != null && targetController.IsFightingDefensively) ? 2 : 0);

            if (protection.DeflectionAcBonus > 0)
            {
                touchAC += protection.DeflectionAcBonus;
                result.ProtectionAcBonus = protection.DeflectionAcBonus;
            }

            int animateRopeRangePenalty = 0;
            int animateRopeIncrement = 0;
            if (isRanged
                && spell != null
                && string.Equals(spell.SpellId, "animate_rope", System.StringComparison.Ordinal)
                && casterController != null
                && targetController != null)
            {
                int distanceSquares = SquareGridUtils.GetDistance(casterController.GridPosition, targetController.GridPosition);
                int distanceFeet = Mathf.Max(0, distanceSquares * 5);
                animateRopeIncrement = Mathf.Max(1, Mathf.CeilToInt(distanceFeet / 10f));
                if (animateRopeIncrement > 5)
                {
                    result.RequiredAttackRoll = true;
                    result.IsRangedTouch = true;
                    result.AttackHit = false;
                    result.Success = false;
                    result.NoEffectReason = $"Target is beyond Animate Rope maximum range (increment {animateRopeIncrement}/5).";
                    return result;
                }

                animateRopeRangePenalty = -2 * Mathf.Max(0, animateRopeIncrement - 1);
            }

            int roll = Random.Range(1, 21);
            int total = roll + atkBonus + situationalSpellAttackBonus + fightingDefensivelyPenalty + shootingIntoMeleePenalty + animateRopeRangePenalty;

            result.AttackRoll = roll;
            result.AttackBonus = atkBonus;
            result.SituationalAttackBonus = situationalSpellAttackBonus;
            result.SituationalAttackBonusSource = situationalSpellAttackSource;
            result.AttackTotal = total;
            result.TouchAC = touchAC;
            result.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            result.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            result.PreciseShotNegated = preciseShotNegated;
            result.TargetFightingDefensivelyACBonus = (targetController != null && targetController.IsFightingDefensively) ? 2 : 0;
            result.RangeIncrementNumber = animateRopeIncrement;
            result.RangeIncrementPenalty = animateRopeRangePenalty;

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

        // ========== PROTECTION FROM ALIGNMENT: MENTAL CONTROL BLOCK ==========
        if (result.AttackHit && IsBlockedByProtectionFromAlignment(targetController, casterController, spell))
        {
            result.MindAffectingBlockedByProtection = true;
            result.Success = false;
            result.TargetHPBefore = targetStats != null ? targetStats.CurrentHP : 0;
            result.TargetHPAfter = result.TargetHPBefore;
            result.NoEffectReason = "Protection from alignment blocks mental control from this source.";
            return result;
        }

        // ========== MIND-AFFECTING IMMUNITY ==========
        if (result.AttackHit && spell.IsMindAffecting && IsImmuneToMindAffecting(targetStats))
        {
            result.MindAffectingImmunityBlocked = true;
            result.Success = false;
            return result;
        }

        // ========== SPELL-SPECIFIC TARGET RESTRICTIONS ==========
        if (result.AttackHit && IsDisruptUndeadSpell(spell) && !IsUndeadTarget(targetStats))
        {
            result.Success = false;
            result.NoEffectReason = "Disrupt Undead has no effect on living creatures.";
            result.TargetHPBefore = targetStats.CurrentHP;
            result.TargetHPAfter = targetStats.CurrentHP;
            return result;
        }

        // Breaking charm: if the charm caster makes a hostile action against their charmed target,
        // the charm ends immediately.
        if (result.AttackHit
            && casterController != null
            && targetController != null
            && (spell.EffectType == SpellEffectType.Damage || spell.EffectType == SpellEffectType.Debuff)
            && GameManager.Instance != null)
        {
            GameManager.Instance.BreakCharmOnHostileAction(casterController, targetController);
            GameManager.Instance.BreakFascinationOnHostileAction(casterController, targetController, "spell attack");
            Vector2Int noiseOrigin = targetController != null ? targetController.GridPosition : casterController.GridPosition;
            GameManager.Instance.BreakFascinationFromLoudNoise(casterController, noiseOrigin, radiusSquares: 6);
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
                int saveMod = GetSaveModifier(targetStats, spell, protection, casterController, targetController, out int protectionSaveBonus);
                result.SaveRoll = saveRoll;
                result.SaveMod = saveMod;
                result.ProtectionSaveBonus = protectionSaveBonus;
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
                int effectiveCasterLevel = casterStats != null ? Mathf.Max(1, casterStats.GetCasterLevel()) : 1;
                int missileCount = Mathf.Min(5, 1 + (effectiveCasterLevel - 1) / 2);
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

            int effectiveRangeSquares = spell.GetRangeSquaresForCasterLevel(casterStats != null ? casterStats.GetCasterLevel() : 0);
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

    private static bool ShouldApplyShockingGraspMetalBonus(
        SpellData spell,
        CharacterController targetController,
        CharacterStats targetStats,
        out string bonusSource)
    {
        bonusSource = string.Empty;
        if (spell == null || !string.Equals(spell.SpellId, "shocking_grasp", System.StringComparison.Ordinal))
            return false;

        if (IsTargetWearingMetalArmor(targetController))
        {
            bonusSource = "Shocking Grasp +3 vs metal armor";
            return true;
        }

        if (IsTargetComposedOfMetal(targetController, targetStats))
        {
            bonusSource = "Shocking Grasp +3 vs metal body";
            return true;
        }

        return false;
    }

    private static bool IsTargetWearingMetalArmor(CharacterController targetController)
    {
        if (targetController == null)
            return false;

        InventoryComponent invComponent = targetController.GetComponent<InventoryComponent>();
        ItemData armor = invComponent != null ? invComponent.CharacterInventory?.GetEquipped(EquipSlot.Armor) : null;
        if (armor == null)
            return false;

        if (armor.ArmorMaterial == ArmorMaterialType.Metal || armor.ArmorMaterial == ArmorMaterialType.Mixed)
            return true;

        // Backward-compatible fallback in case legacy items were not tagged yet.
        string armorName = string.IsNullOrWhiteSpace(armor.Name) ? string.Empty : armor.Name.ToLowerInvariant();
        string armorId = string.IsNullOrWhiteSpace(armor.Id) ? string.Empty : armor.Id.ToLowerInvariant();
        return armorName.Contains("chain")
            || armorName.Contains("scale")
            || armorName.Contains("plate")
            || armorName.Contains("banded")
            || armorName.Contains("splint")
            || armorId.Contains("chain")
            || armorId.Contains("scale")
            || armorId.Contains("plate")
            || armorId.Contains("banded")
            || armorId.Contains("splint");
    }

    private static bool IsTargetComposedOfMetal(CharacterController targetController, CharacterStats targetStats)
    {
        MaterialComposition composition = MaterialComposition.Unknown;

        if (targetController != null && targetController.Stats != null)
            composition = targetController.Stats.MaterialComposition;

        if (composition == MaterialComposition.Unknown && targetStats != null)
            composition = targetStats.MaterialComposition;

        return composition == MaterialComposition.Metal || composition == MaterialComposition.Mixed;
    }

    private static bool IsMagicMissileSpell(SpellData spell)
    {
        return spell != null && spell.SpellId == "magic_missile";
    }

    private static bool IsDisruptUndeadSpell(SpellData spell)
    {
        return spell != null && spell.SpellId == "disrupt_undead";
    }

    private static bool IsUndeadTarget(CharacterStats targetStats)
    {
        if (targetStats == null || string.IsNullOrWhiteSpace(targetStats.CreatureType))
            return false;

        return string.Equals(targetStats.CreatureType.Trim(), "undead", System.StringComparison.OrdinalIgnoreCase);
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

    private static string GetActiveAlignmentProtectionSourceName(CharacterController targetController)
    {
        if (targetController == null)
            return string.Empty;

        StatusEffectManager statusMgr = targetController.GetComponent<StatusEffectManager>();
        if (statusMgr == null || statusMgr.ActiveEffects == null)
            return string.Empty;

        for (int i = 0; i < statusMgr.ActiveEffects.Count; i++)
        {
            ActiveSpellEffect effect = statusMgr.ActiveEffects[i];
            if (effect == null)
                continue;

            AlignmentProtectionType protectionType = effect.ProtectionAgainstAlignment;
            if (protectionType == AlignmentProtectionType.None && effect.Spell != null)
                AlignmentProtectionRules.TryGetProtectionTypeForSpell(effect.Spell.SpellId, out protectionType);

            if (protectionType == AlignmentProtectionType.None)
                continue;

            if (effect.Spell != null && !string.IsNullOrWhiteSpace(effect.Spell.Name))
                return effect.Spell.Name;

            return "Protection from Alignment";
        }

        return string.Empty;
    }

    private static int GetCasterLevelForSpellResistanceCheck(CharacterStats casterStats)
    {
        if (casterStats == null)
            return 0;

        int casterLevel = casterStats.GetCasterLevel();
        if (casterLevel > 0)
            return casterLevel;

        return Mathf.Max(1, casterStats.EffectiveCharacterLevel);
    }

    /// <summary>
    /// Check if target is immune to this spell due to Protection from [Alignment].
    /// This uses SpellData.BlockedByProtectionFromAlignment instead of the broader
    /// SpellData.IsMindAffecting flag so only curated mental-control spells are blocked.
    /// </summary>
    private static bool IsBlockedByProtectionFromAlignment(
        CharacterController target,
        CharacterController caster,
        SpellData spell)
    {
        if (spell == null || !spell.BlockedByProtectionFromAlignment)
            return false;

        if (target == null || caster == null || caster.Stats == null)
            return false;

        AlignmentProtectionBenefits protection = AlignmentProtectionRules.GetBenefitsAgainst(target, caster.Stats.CharacterAlignment);
        return protection.HasMatch && protection.BlocksMentalControl;
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
    private static bool HasCondition(CharacterStats stats, CombatConditionType condition)
    {
        if (stats == null || stats.ActiveConditions == null)
            return false;

        CombatConditionType normalized = ConditionRules.Normalize(condition);
        for (int i = 0; i < stats.ActiveConditions.Count; i++)
        {
            StatusEffect active = stats.ActiveConditions[i];
            if (active == null)
                continue;

            if (ConditionRules.Normalize(active.Type) == normalized)
                return true;
        }

        return false;
    }

    private static int GetSaveModifier(
        CharacterStats stats,
        SpellData spell,
        AlignmentProtectionBenefits protection,
        CharacterController casterController,
        CharacterController targetController,
        out int protectionSaveBonus)
    {
        protectionSaveBonus = 0;

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

        // D&D 3.5e Charm Person: +5 bonus on save if threatened/attacked by caster side.
        if (spell.SavingThrowType == "Will"
            && string.Equals(spell.SpellId, "charm_person", System.StringComparison.Ordinal)
            && IsBeingThreatenedBy(targetController, casterController))
        {
            baseSave += 5;
        }

        if (protection.HasMatch && protection.ResistanceSaveBonus > 0)
        {
            protectionSaveBonus = protection.ResistanceSaveBonus;
            baseSave += protectionSaveBonus;
        }

        return baseSave;
    }

    /// <summary>
    /// Returns true when a target would receive the Charm Person "threatened or attacked" save bonus.
    /// </summary>
    public static bool IsBeingThreatenedBy(CharacterController target, CharacterController caster)
    {
        if (target == null || caster == null || target.Stats == null || caster.Stats == null)
            return false;

        GameManager gm = GameManager.Instance;
        if (gm == null)
            return false;

        if (!gm.IsEnemyTeamForAI(caster, target))
            return false;

        List<CharacterController> allCombatants = GetAllCombatCharactersSnapshot();
        if (allCombatants == null || allCombatants.Count == 0)
            return false;

        // Direct melee threat by the caster.
        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(target.GridPosition, target, allCombatants);
        for (int i = 0; i < threateningEnemies.Count; i++)
        {
            CharacterController threat = threateningEnemies[i];
            if (threat == null || threat.Stats == null || threat.Stats.IsDead)
                continue;

            if (threat == caster)
                return true;

            if (threat.Team == caster.Team)
                return true;
        }

        // If the target has already taken damage while hostile to the caster side this encounter,
        // treat as "being attacked" for the charm save bonus.
        if (target.Stats.CurrentHP < target.Stats.TotalMaxHP && target.Team != caster.Team)
            return true;

        return false;
    }
}
