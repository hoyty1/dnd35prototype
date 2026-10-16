using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

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
    /// <summary>DiceService context of the touch attack roll (a scenario can force it; the draw is the same as DiceRoller.D20).</summary>
    public const string TouchAttackContext = "Spell touch attack";
    /// <summary>DiceService context of the target's saving throw.</summary>
    public const string SavingThrowContext = "Spell saving throw";

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
        CharacterController targetController,
        LesserGlobeOfInvulnerabilityAreaEffect globeAgainstTouch = null)
    {
        var result = new SpellResult();
        result.Spell = spell;
        result.CasterName = casterStats.CharacterName;
        result.TargetName = targetStats.CharacterName;
        result.DamageType = spell.DamageType ?? "";

        var parsedSpellDamageTypes = DamageTextUtils.ParseDamageTypes(spell.DamageType);
        result.DamageTypeSummary = DamageTextUtils.FormatDamageTypes(parsedSpellDamageTypes);
        result.Success = true;
        // The cast's metamagic, else the one stamped on the cast's spell clone (SpellCaster.ApplyMetamagicToSpellData,
        // a prepared metamagic slot's spell): one fallback with SpellSaveDCRules and every custom resolver's dice
        // (SpellDiceRules.MetamagicOf; batch 11 step 19, SPL-038).
        metamagic = SpellDiceRules.MetamagicOf(spell, metamagic);
        result.Metamagic = metamagic;

        bool isEmpowered = metamagic != null && metamagic.Has(MetamagicFeatId.EmpowerSpell);
        bool isMaximized = metamagic != null && metamagic.Has(MetamagicFeatId.MaximizeSpell);
        bool isHeightened = metamagic != null && metamagic.Has(MetamagicFeatId.HeightenSpell);

        // Log active metamagic for debugging
        if (isEmpowered || isMaximized || isHeightened)
        {
            var mmParts = new System.Collections.Generic.List<string>();
            if (isEmpowered) mmParts.Add("Empower (×1.5 damage/healing)");
            if (isMaximized) mmParts.Add("Maximize (max dice)");
            if (isHeightened) mmParts.Add($"Heighten (effective level {metamagic.HeightenToLevel})");
            Debug.Log($"[Metamagic] Applying metamagic to {spell.Name}: {string.Join(", ", mmParts)}");
        }

        Alignment sourceAlignment = casterStats != null ? casterStats.CharacterAlignment : Alignment.None;
        AlignmentProtectionBenefits protection = AlignmentProtectionRules.GetBenefitsAgainst(targetController, sourceAlignment);
        result.ProtectionSourceName = GetActiveAlignmentProtectionSourceName(targetController);
        if (!string.IsNullOrEmpty(protection.SourceSpellName))
            result.ProtectionSourceName = protection.SourceSpellName;

        // A deafened caster's 20% verbal spell failure (PHB p.307) is one roll per casting, made in the cast core's Failure
        // stage (CastComponentRules.TryDeafenedFailure; batch 11 step 13, SPL-150), not here: this runs once per target of
        // an area spell and again for every later hit of a lasting effect (the Flaming Sphere).

        // ========== POSITIVE / NEGATIVE ENERGY (Cure and Inflict, SPL-005) ==========
        // On an undead a cure's positive energy is damage (a touch attack against an unwilling target, spell
        // resistance, Will half; PHB p.215) and an inflict spell's negative energy cures it (PHB p.244), which the
        // undead accepts: no touch attack roll and no save. A construct is unaffected (SpellDiceRules.OutcomeFor).
        SpellEnergyOutcome energyOutcome = SpellDiceRules.OutcomeFor(spell, targetStats);
        bool harmsUndead = energyOutcome == SpellEnergyOutcome.HarmsUndead;
        bool healsUndead = energyOutcome == SpellEnergyOutcome.HealsUndead;
        result.EnergyOutcome = energyOutcome;
        if (harmsUndead)
        {
            result.DamageType = "positive";
            parsedSpellDamageTypes = DamageTextUtils.ParseDamageTypes("positive");
            result.DamageTypeSummary = DamageTextUtils.FormatDamageTypes(parsedSpellDamageTypes);
        }

        // ========== ATTACK ROLL (touch attacks) ==========
        // AoE spells do not use touch attack rolls.
        bool isAoESpell = spell.TargetType == SpellTargetType.Area;
        bool usesTouchAttack = spell.IsMeleeTouchSpell() || spell.IsRangedTouchSpell();
        if (healsUndead)
            forceFriendlyTouchNoRoll = true;

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

            // A touch attack is an attack roll: the racial bonus against the target's kind applies (PHB p.15, p.17; CHR-019).
            int atkBonus = CombatCalculationService.TouchAttackBonus(casterStats, isRanged)
                + RacialTraitRules.AttackBonusAgainst(casterStats, targetStats);

            int situationalSpellAttackBonus = 0;
            string situationalSpellAttackSource = string.Empty;
            if (ShouldApplyShockingGraspMetalBonus(spell, targetController, targetStats, out string shockingGraspBonusSource))
            {
                situationalSpellAttackBonus += 3;
                situationalSpellAttackSource = shockingGraspBonusSource;
            }

            int fightingDefensivelyPenalty = (casterController != null && casterController.IsFightingDefensively) ? CombatCalculationService.FightingDefensivelyAttackPenalty : 0;
            bool preciseShotNegated = false;
            int shootingIntoMeleePenalty = 0;
            if (isRanged)
            {
                shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(casterController, targetController, out preciseShotNegated);
            }

            int touchAC = SpellcastingComponent.GetTouchAC(targetStats)
                + ((targetController != null && targetController.IsFightingDefensively) ? CombatCalculationService.FightingDefensivelyACBonus : 0)
                + RacialTraitRules.KindDodgeACBonusAgainst(targetStats, casterStats); // dwarf and gnome vs giants (CHR-019)

            // The ward's deflection does not stack with the target's own deflection: only the excess applies.
            int protectionDeflection = AlignmentProtectionRules.DeflectionAcIncrease(protection, targetStats);
            if (protectionDeflection > 0)
            {
                touchAC += protectionDeflection;
                result.ProtectionAcBonus = protectionDeflection;
            }

            int animateRopeRangePenalty = 0;
            int animateRopeIncrement = 0;
            if (isRanged
                && spell != null
                && string.Equals(spell.SpellId, SpellNames.ANIMATE_ROPE, System.StringComparison.Ordinal)
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

            int roll = DiceService.D20(TouchAttackContext);
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
            result.TargetFightingDefensivelyACBonus = (targetController != null && targetController.IsFightingDefensively) ? CombatCalculationService.FightingDefensivelyACBonus : 0;
            result.RangeIncrementNumber = animateRopeIncrement;
            result.RangeIncrementPenalty = animateRopeRangePenalty;

            result.AttackHit = CombatCalculationService.IsHit(roll, total, touchAC);

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

        // ========== CONCEALMENT MISS CHANCE (applies after successful touch/ranged touch attack roll) ==========
        if (result.AttackHit && result.RequiredAttackRoll && casterController != null && targetController != null)
        {
            int targetMissChance = targetController.GetMissChance(casterController, result.IsRangedTouch);
            int blindedCasterMissChance = casterController.HasCondition(CombatConditionType.Blinded) ? CombatCalculationService.BlindedAttackerMissChance : 0;
            int missChance = Mathf.Max(targetMissChance, blindedCasterMissChance);

            if (missChance > 0)
            {
                int concealmentRoll = DiceRoller.D100();
                if (CombatCalculationService.ConcealmentMiss(concealmentRoll, missChance))
                {
                    result.AttackHit = false;
                    result.Success = false;
                    result.NoEffectReason = $"Attack misses due to concealment ({concealmentRoll} ≤ {missChance}).";
                    return result;
                }
            }
        }

        // ========== GLOBE OF INVULNERABILITY (a melee touch on a creature inside it) ==========
        // The cast core decides every other spell at a covered creature before this call (GameManager.CastStageGlobe). A
        // melee touch is still made: a touch that lands discharges the spell, which fails to affect the creature inside
        // the globe (PHB p.236, p.141; delegated RAW reading 2026-10-14, SPL-141). A missed touch returned above.
        if (result.AttackHit && globeAgainstTouch != null)
        {
            result.Success = false;
            result.BlockedByGlobe = true;
            result.TargetHPBefore = targetStats != null ? targetStats.CurrentHP : 0;
            result.TargetHPAfter = result.TargetHPBefore;
            string touchText = result.RequiredAttackRoll
                ? $"The touch lands (d20 {result.AttackRoll}, total {result.AttackTotal} vs touch AC {result.TouchAC})"
                : "The touch lands";
            result.NoEffectReason = $"{touchText}, but {LesserGlobeOfInvulnerabilityAreaEffect.DescribeBlock(globeAgainstTouch)} around {targetStats?.CharacterName} keeps the spell out: it is discharged with no effect (PHB p.236).";
            return result;
        }

        // ========== SILENCE (a defense against sonic spells) ==========
        // PHB p.279: silence stops all sound in its area and is a defense against sonic attacks (its example, a horn of
        // blasting, deals sonic damage), so a sonic spell has no effect on a creature under it: no damage, no rider.
        // Only the creature a Silence is centred on is known to be under it (Stats.SilenceActive; the 20-ft emanation is
        // not modelled, SPL-166). Delegated RAW reading 2026-10-14 (SPL-142).
        if (result.AttackHit && spell.HasDescriptor(SpellDescriptor.Sonic) && targetStats != null && targetStats.SilenceActive)
        {
            result.Success = false;
            result.SonicBlockedBySilence = true;
            result.TargetHPBefore = targetStats.CurrentHP;
            result.TargetHPAfter = targetStats.CurrentHP;
            result.NoEffectReason = $"{targetStats.CharacterName} is inside a Silence: no sound reaches it, so the sonic spell has no effect (PHB p.279).";
            return result;
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
            result.NoEffectReason = targetStats != null && targetStats.IsMindless
                ? "Target is mindless and immune to mind-affecting effects."
                : "Target is immune to mind-affecting effects.";
            return result;
        }

        // ========== SPELL-SPECIFIC TARGET RESTRICTIONS ==========
        if (result.AttackHit && energyOutcome == SpellEnergyOutcome.NoEffect)
        {
            result.Success = false;
            result.NoEffectReason = spell.Energy == SpellEnergy.Positive
                ? $"{spell.Name} has no effect: its positive energy cures only living creatures (PHB p.215)."
                : $"{spell.Name} has no effect: constructs are immune to necromancy effects (MM p.307).";
            result.TargetHPBefore = targetStats.CurrentHP;
            result.TargetHPAfter = targetStats.CurrentHP;
            return result;
        }

        if (result.AttackHit && IsDisruptUndeadSpell(spell) && !IsUndeadTarget(targetStats))
        {
            result.Success = false;
            result.NoEffectReason = "Disrupt Undead has no effect on living creatures.";
            result.TargetHPBefore = targetStats.CurrentHP;
            result.TargetHPAfter = targetStats.CurrentHP;
            return result;
        }

        if (result.AttackHit && string.Equals(spell.SpellId, SpellNames.DAZE_MONSTER, System.StringComparison.Ordinal))
        {
            if (!IsLivingCreature(targetStats))
            {
                result.Success = false;
                result.NoEffectReason = "Immune (not a living creature).";
                result.TargetHPBefore = targetStats.CurrentHP;
                result.TargetHPAfter = targetStats.CurrentHP;
                return result;
            }

            int hitDice = targetStats.GetHitDice();
            if (hitDice > 6)
            {
                result.Success = false;
                result.NoEffectReason = $"Immune (7+ HD). Target has {hitDice} HD.";
                result.TargetHPBefore = targetStats.CurrentHP;
                result.TargetHPAfter = targetStats.CurrentHP;
                return result;
            }
        }

        if (result.AttackHit && string.Equals(spell.SpellId, SpellNames.TOUCH_OF_IDIOCY, System.StringComparison.Ordinal))
        {
            if (!IsLivingCreature(targetStats))
            {
                result.Success = false;
                result.NoEffectReason = "Immune (not a living creature).";
                result.TargetHPBefore = targetStats.CurrentHP;
                result.TargetHPAfter = targetStats.CurrentHP;
                return result;
            }
        }

        if (result.AttackHit && string.Equals(spell.SpellId, SpellNames.HIDEOUS_LAUGHTER, System.StringComparison.Ordinal))
        {
            int effectiveInt = targetStats.EffectiveINTScore;
            if (targetStats.IsMindless || effectiveInt <= 2)
            {
                result.Success = false;
                result.NoEffectReason = $"Immune (Int 2 or less). Target Int: {effectiveInt}.";
                result.TargetHPBefore = targetStats.CurrentHP;
                result.TargetHPAfter = targetStats.CurrentHP;
                return result;
            }
        }

        // Breaking charm: if the charm caster makes a hostile action against their charmed target,
        // the charm ends immediately.
        bool isHostileEffect = harmsUndead
            || (spell.EffectType == SpellEffectType.Damage && !healsUndead)
            || spell.EffectType == SpellEffectType.Debuff;
        if (result.AttackHit
            && casterController != null
            && targetController != null
            && isHostileEffect
            && GameManager.Instance != null)
        {
            GameManager.Instance.BreakCharmOnHostileAction(casterController, targetController);
            GameManager.Instance.BreakCommandUndeadOnHostileAction(casterController, targetController);
            GameManager.Instance.BreakFascinationOnHostileAction(casterController, targetController, "spell attack");
            Vector2Int noiseOrigin = targetController != null ? targetController.GridPosition : casterController.GridPosition;
            GameManager.Instance.BreakFascinationFromLoudNoise(casterController, noiseOrigin, radiusSquares: 6);
        }

        // ========== SPELL RESISTANCE ==========
        // A cure is "Yes (harmless)" against the living, but an undead applies spell resistance against it (PHB p.215).
        if (result.AttackHit && (spell.SpellResistanceApplies || harmsUndead) && targetStats != null && targetStats.SpellResistance > 0)
        {
            // The one spell resistance rule (batch 11 step 18, SPL-122): d20 + caster level + Spell Penetration.
            SRResult sr = SpellSaveResolver.RollSpellResistance(
                casterStats != null ? CastCasterLevel(casterController, casterStats, spell) : 0,
                FeatManager.GetSpellPenetrationBonus(casterStats), targetStats.SpellResistance);
            result.SpellResistanceChecked = true;
            result.SpellResistanceValue = sr.TargetSR;
            result.SpellResistanceRoll = sr.Roll;
            result.SpellResistanceTotal = sr.Total;
            result.SpellResistancePassed = sr.Overcame;

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
        // A cure harming an undead allows a Will save for half (PHB p.215), and the undead never forgoes it; an
        // inflict spell curing an undead is accepted without a save (PHB p.177: a creature may forgo its save).
        bool allowsSave = harmsUndead || (spell.AllowsSavingThrow && !healsUndead);
        string saveType = harmsUndead ? "Will" : spell.SavingThrowType;
        bool saveHalves = harmsUndead || spell.SaveHalves;
        result.SaveHalves = saveHalves;
        if (harmsUndead)
            forceTargetToFailSave = false;

        if (allowsSave && result.AttackHit)
        {
            result.RequiredSave = true;
            result.SaveType = saveType;

            // One DC rule for every cast path (SpellSaveDCRules, SPL-001): a pre-baked DC (scroll, wand, imbued
            // spell) is kept; otherwise 10 + the casting class's (or heightened) level + its key ability + Spell Focus.
            SpellSaveDCBreakdown dc = casterController != null
                ? SpellSaveDCRules.Explain(casterController, spell, metamagic)
                : SpellSaveDCRules.Explain(casterStats, spell, metamagic);
            result.SaveDC = dc.Total;
            Debug.Log($"[SpellCaster] {spell.Name} save {dc}");

            if (forceTargetToFailSave)
            {
                result.SaveRoll = 1;
                result.SaveMod = 0;
                result.SaveTotal = 1;
                result.SaveSucceeded = false;
            }
            else
            {
                int saveRoll = DiceService.D20(SavingThrowContext);
                SaveContext saveContext = SaveContext.ForSpell(spell, casterStats, casterController);
                int saveMod = GetSaveModifier(
                    targetStats,
                    spell,
                    saveType,
                    saveContext,
                    targetController,
                    out int protectionSaveBonus,
                    out int situationalSaveBonus,
                    out string situationalSaveSource);
                result.SaveRoll = saveRoll;
                result.SaveMod = saveMod;
                result.ProtectionSaveBonus = protectionSaveBonus;
                result.SituationalSaveBonus = situationalSaveBonus;
                result.SituationalSaveBonusSource = situationalSaveSource;
                // A countersong's Perform result replaces the save against a sonic or language-dependent spell when higher
                // (PHB p.29; CHR-026).
                result.SaveTotal = BardicMusicRules.ApplyCountersong(targetController, saveRoll + saveMod, saveContext);
                result.SaveSucceeded = result.SaveTotal >= result.SaveDC;
            }
        }

        // ========== DAMAGE ==========
        // A spell whose custom handler deals the damage skips this branch, or the target takes both (SPL-124).
        // Dice, caster-level bonus and caps come from SpellDiceRules (SPL-005); a cure harming an undead rolls its
        // cure dice here as positive-energy damage.
        int casterLevel = SpellDiceRules.CasterLevelFor(casterStats, spell);
        result.CasterLevel = casterLevel;
        bool dealsDamage = harmsUndead || (spell.EffectType == SpellEffectType.Damage && !healsUndead);
        bool heals = healsUndead || (spell.EffectType == SpellEffectType.Healing && !harmsUndead);

        if (dealsDamage && result.AttackHit && !spell.DamageResolvedByHandler)
        {
            result.TargetHPBefore = targetStats.CurrentHP;

            if (spell.AutoHit && spell.MissileCount > 0)
            {
                // Magic Missile: 1 + (CL-1)/2 missiles, max 5, at the cast's caster level (a scroll's or wand's own; SPL-016)
                int effectiveCasterLevel = CastCasterLevel(casterController, casterStats, spell);
                int missileCount = Mathf.Min(5, 1 + (effectiveCasterLevel - 1) / 2);
                result.MissileCount = missileCount;
                result.MissileDamages = new int[missileCount];
                int totalDmg = 0;

                // Each missile is its own roll of the spell's die + bonus; Empower multiplies each missile's result by
                // one-half (PHB p.93: "roll 1d4+1 and multiply the result by 1-1/2 for each missile"), Maximize sets
                // its die to the maximum (SpellDiceRules.Roll; SPL-097).
                SpellDice missileDice = SpellDice.Of(1, spell.DamageDice, spell.BonusDamage);
                int empowerTotal = 0;
                for (int i = 0; i < missileCount; i++)
                {
                    SpellDiceRoll missile = SpellDiceRules.Roll(missileDice, metamagic, SpellDiceRules.DamageDieContext);
                    int missileDmg = missile.Total;
                    empowerTotal += missile.EmpowerBonus;

                    result.MissileDamages[i] = missileDmg;
                    totalDmg += missileDmg;
                }

                if (isEmpowered)
                {
                    result.EmpowerBonus = empowerTotal;
                    Debug.Log($"[Metamagic] Empower Spell (missiles): +{empowerTotal} (each missile x1.5) = {totalDmg}");
                }

                result.DamageRolled = totalDmg;
                result.DamageDealt = totalDmg;
            }
            else
            {
                SpellDice dice = SpellDiceRules.Effect(spell, casterLevel);
                result.DiceCount = dice.Count;
                result.DiceSides = dice.Sides;
                result.DiceBonus = dice.Bonus;

                SpellDiceRoll roll = SpellDiceRules.Roll(dice, metamagic, SpellDiceRules.DamageDieContext);
                result.DamageRolls = roll.Rolls;
                int baseDmg = roll.Total;

                if (isEmpowered)
                {
                    result.EmpowerBonus = roll.EmpowerBonus;
                    Debug.Log($"[Metamagic] Empower Spell: base damage {roll.Base} + {roll.EmpowerBonus} (half of the normal roll) = {baseDmg}");
                }
                if (isMaximized)
                    Debug.Log($"[Metamagic] Maximize Spell: all {dice.Count}d{dice.Sides} dice set to max ({dice.Sides} each)");

                result.DamageRolled = baseDmg;

                // A partial save whose damage always lands (Sound Burst, PHB p.281) leaves the damage whole (SPL-142).
                bool saveReducesDamage = !spell.DamageUnaffectedBySave;
                if (result.RequiredSave && result.SaveSucceeded && saveHalves)
                    result.DamageDealt = Mathf.Max(0, result.DamageRolled / 2);
                else if (result.RequiredSave && result.SaveSucceeded && !saveHalves && saveReducesDamage)
                    result.DamageDealt = 0;
                else
                    result.DamageDealt = Mathf.Max(0, result.DamageRolled);
            }

            if (result.DamageDealt > 0)
                result.DamageDealt = Mathf.Max(1, result.DamageDealt);

            int effectiveRangeSquares = spell.GetRangeSquaresForCasterLevel(CastCasterLevel(casterController, casterStats, spell));
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
                SourceName = spell.Name,
                // Fire Shield turns a successful Reflex save for half into no damage (PHB p.230; SPL-004).
                SavedForHalf = result.RequiredSave && result.SaveSucceeded && saveHalves
                    && saveType == "Reflex"
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
        // A healing spell, or an inflict spell curing an undead (its inflict dice are the cure, PHB p.244). A spell whose
        // ApplySpellBuff branch does the healing (Heal, Resurrection) heals nothing here, or the target is healed twice (SPL-037).
        if (heals && result.AttackHit && !spell.HealingResolvedByHandler)
        {
            result.TargetHPBefore = targetStats.CurrentHP;

            SpellDice dice = SpellDiceRules.Effect(spell, casterLevel);
            result.DiceCount = dice.Count;
            result.DiceSides = dice.Sides;
            result.DiceBonus = dice.Bonus;

            SpellDiceRoll healRoll = SpellDiceRules.Roll(dice, metamagic, SpellDiceRules.HealingDieContext);
            result.HealRolls = healRoll.Rolls;
            int healDice = 0;
            for (int i = 0; i < healRoll.Rolls.Length; i++)
                healDice += healRoll.Rolls[i];
            result.HealRolled = healDice;

            int totalHeal = healRoll.Total;

            if (isEmpowered)
            {
                result.EmpowerBonus = healRoll.EmpowerBonus;
                Debug.Log($"[Metamagic] Empower Spell: base healing {healRoll.Base} + {healRoll.EmpowerBonus} (half of the normal roll) = {totalHeal}");
            }
            if (isMaximized)
                Debug.Log($"[Metamagic] Maximize Spell: all {dice.Count}d{dice.Sides} heal dice set to max ({dice.Sides} each)");

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

            if (spell.SpellId == SpellNames.MAGE_ARMOR)
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
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.SHOCKING_GRASP, System.StringComparison.Ordinal))
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

        InventoryComponent invComponent = targetController.InventoryComp;
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
        return spell != null && spell.SpellId == SpellNames.MAGIC_MISSILE;
    }

    private static bool IsDisruptUndeadSpell(SpellData spell)
    {
        return spell != null && spell.SpellId == SpellNames.DISRUPT_UNDEAD;
    }

    private static bool IsUndeadTarget(CharacterStats targetStats)
    {
        if (targetStats == null || string.IsNullOrWhiteSpace(targetStats.CreatureType))
            return false;

        return string.Equals(targetStats.CreatureType.Trim(), "undead", System.StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsLivingCreature(CharacterStats targetStats)
    {
        if (targetStats == null)
            return false;

        string creatureType = string.IsNullOrWhiteSpace(targetStats.CreatureType)
            ? string.Empty
            : targetStats.CreatureType.Trim();

        if (creatureType.Equals("Undead", System.StringComparison.OrdinalIgnoreCase)
            || creatureType.Equals("Construct", System.StringComparison.OrdinalIgnoreCase))
            return false;

        return !targetStats.IsDead;
    }

    private static bool HasActiveShieldSpell(CharacterController targetController)
    {
        if (targetController == null)
            return false;

        StatusEffectManager statusMgr = targetController.StatusEffectManager;
        if (statusMgr != null && statusMgr.ActiveEffects != null)
        {
            for (int i = 0; i < statusMgr.ActiveEffects.Count; i++)
            {
                ActiveSpellEffect effect = statusMgr.ActiveEffects[i];
                if (effect?.Spell == null)
                    continue;

                if (effect.Spell.SpellId == SpellNames.SHIELD && effect.RemainingRounds > 0)
                    return true;
            }
        }

        SpellcastingComponent spellComp = targetController.Spellcasting;
        if (spellComp != null
            && spellComp.ActiveBuffs != null
            && spellComp.ActiveBuffs.TryGetValue(SpellNames.SHIELD, out int rounds)
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
    /// Quicken (change action type), and stamps the metamagic on the clone (<see cref="SpellData.MetamagicDataRef"/>).
    /// Empower/Maximize are applied to the dice where they are rolled (SpellDiceRules.Roll) and Heighten where a level
    /// is read (SpellSaveDCRules.GetEffectiveSpellLevel).
    /// </summary>
    public static void ApplyMetamagicToSpellData(SpellData spell, MetamagicData metamagic)
    {
        if (metamagic == null || !metamagic.HasAnyMetamagic) return;

        // Enlarge Spell: double range AND area dimensions (cone length, line length, burst/spread size)
        // D&D 3.5e: Enlarge doubles close/medium/long range. We also scale AoE size for cones/lines/areas.
        if (metamagic.Has(MetamagicFeatId.EnlargeSpell))
        {
            if (spell.RangeSquares > 0)
            {
                spell.RangeSquares *= 2;
                if (spell.RangeIncreaseSquares > 0)
                    spell.RangeIncreaseSquares *= 2;
                Debug.Log($"[Metamagic] Enlarge Spell: range doubled to {spell.RangeSquares} squares (scaling +{spell.RangeIncreaseSquares}/{spell.RangeIncreasePerLevels} lv)");
            }
            // Also double AoE dimensions (cone length, line length, burst size)
            if (spell.AoESizeSquares > 0)
            {
                spell.AoESizeSquares *= 2;
                Debug.Log($"[Metamagic] Enlarge Spell: AoESizeSquares doubled to {spell.AoESizeSquares} sq");
            }
            if (spell.AreaRadius > 0)
            {
                spell.AreaRadius *= 2;
                Debug.Log($"[Metamagic] Enlarge Spell: AreaRadius doubled to {spell.AreaRadius} sq");
            }
        }

        // Extend Spell: double duration
        // D&D 3.5e PHB p.91: Does NOT affect instantaneous, concentration, or permanent durations.
        if (metamagic.Has(MetamagicFeatId.ExtendSpell))
        {
            // The one Extend test, also MetamagicData.CanExtend's offer (SpellDurationRules.IsExtendable; SPL-095).
            bool canExtend = SpellDurationRules.IsExtendable(spell);

            if (canExtend && spell.DurationType == DurationType.Concentration)
            {
                // "Concentration + N rounds": only the rounds after the concentration ends double (PHB p.94, p.176;
                // delegated RAW reading 2026-10-14, SpellDurationRules.IsExtendable); ConcentrationRules.TailRounds reads them.
                spell.ConcentrationTailRounds *= 2;
                Debug.Log($"[Metamagic] Extend Spell: rounds after concentration doubled to {spell.ConcentrationTailRounds}{(spell.ConcentrationTailScalesWithLevel ? "/level" : "")}");
            }
            else if (canExtend)
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
            else
            {
                Debug.Log($"[Metamagic] Extend Spell: skipped (duration type: {spell.DurationType})");
            }
        }

        // Widen Spell: double all area dimensions
        // D&D 3.5e PHB p.92: "Any numeric measurements of the spell's area increase by 100%."
        if (metamagic.Has(MetamagicFeatId.WidenSpell))
        {
            if (spell.AreaRadius > 0)
            {
                spell.AreaRadius *= 2;
                Debug.Log($"[Metamagic] Widen Spell: AreaRadius doubled to {spell.AreaRadius} sq");
            }
            if (spell.AoESizeSquares > 0)
            {
                spell.AoESizeSquares *= 2;
                Debug.Log($"[Metamagic] Widen Spell: AoESizeSquares doubled to {spell.AoESizeSquares} sq");
            }
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

        // The clone carries its metamagic (batch 11 step 19, SPL-038), so every custom resolver that gets the cast's
        // spell reads Empower and Maximize for its dice (SpellDiceRules.RollFor), Extend for the rounds it computes itself
        // (SpellDurationRules.Extended) and Heighten for its level (SpellSaveDCRules, the globe test). It is not a
        // prepared metamagic slot's spell (SpellData.IsPreparedMetamagicSlotSpell stays false), so the slot counts of the
        // spell menu never read it (SpellcastingComponent.CountAvailablePreparedSpell).
        spell.MetamagicDataRef = metamagic;
    }

    private static string GetActiveAlignmentProtectionSourceName(CharacterController targetController)
    {
        if (targetController == null)
            return string.Empty;

        StatusEffectManager statusMgr = targetController.StatusEffectManager;
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

    /// <summary>
    /// The cast's caster level (CasterLevelRules, batch 11 step 14, SPL-016, SPL-017): the open cast's when the caster's
    /// controller is known (a scroll's or wand's own caster level, DMG p.237, p.245), else the stats' rule (the casting
    /// class's caster level, or the Hit Dice of a creature with no casting class, MM p.315). At least 1.
    /// </summary>
    private static int CastCasterLevel(CharacterController casterController, CharacterStats casterStats, SpellData spell)
        => casterController != null && casterController.Stats != null
            ? CasterLevelRules.ForCaster(casterController, spell)
            : CasterLevelRules.ForStats(casterStats, spell);

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
        return targetStats != null && targetStats.IsImmuneToMindAffecting();
    }

    /// <summary>
    /// The target's save modifier against <paramref name="spell"/>: SaveRules.Modifier with the spell-and-caster context
    /// (CHR-018; batch 11 step 18, SPL-018), which also gives the bonuses that depend on the spell and its caster (the
    /// protection ward, Charm Person, Hideous Laughter, Lullaby), so the spell handlers' own saves get the same ones.
    /// <paramref name="protectionSaveBonus"/> is the ward's part and <paramref name="situationalSaveBonus"/> the rest of
    /// the situational part, each logged apart (SpellResult). An unknown save name gives +0 as before.
    /// </summary>
    private static int GetSaveModifier(
        CharacterStats stats,
        SpellData spell,
        string saveType,
        SaveContext context,
        CharacterController targetController,
        out int protectionSaveBonus,
        out int situationalSaveBonus,
        out string situationalSaveSource)
    {
        protectionSaveBonus = 0;
        situationalSaveBonus = 0;
        situationalSaveSource = string.Empty;

        if (stats == null || spell == null)
            return 0;

        if (!SaveRules.TryParse(saveType, out SavingThrowType save))
            return 0;
        int situational = SaveRules.SituationalBonus(stats, targetController, save, context, out string sources,
            out int ward, out _);
        protectionSaveBonus = ward;
        situationalSaveBonus = situational - ward;
        situationalSaveSource = situationalSaveBonus != 0 ? sources : string.Empty;
        return stats.GetSaveTotal(save) + situational;
    }

    /// <summary>
    /// Lullaby (PHB p.249): a creature under the spell takes a -2 penalty on Will saves against sleep effects
    /// (Sleep, Deep Slumber). Returns -2 for such a save, else 0. The -5 on Listen and Spot checks is not modelled
    /// (no Listen or Spot checks in combat). Used by every Will save against a sleep effect: SpellCaster.Cast and the Sleep and Deep
    /// Slumber area resolvers (SPL-037).
    /// </summary>
    public static int LullabySleepSavePenalty(CharacterController target, SpellData spell)
    {
        if (target == null || spell == null || !IsSleepEffect(spell))
            return 0;
        StatusEffectManager effects = target.StatusEffectManager;
        return effects != null && effects.HasEffect(SpellNames.LULLABY) ? -2 : 0;
    }

    /// <summary>The sleep effects among the game's spells (PHB [Sleep]-type enchantments: Sleep, Deep Slumber).</summary>
    public static bool IsSleepEffect(SpellData spell)
    {
        return spell != null
            && (string.Equals(spell.SpellId, SpellNames.SLEEP, System.StringComparison.Ordinal)
                || string.Equals(spell.SpellId, SpellNames.DEEP_SLUMBER, System.StringComparison.Ordinal));
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

    /// <summary>
    /// True for a fear effect (the [Fear] descriptor, PHB p.174), used by SaveContext.ForSpell for the morale bonuses against
    /// fear (halfling, Remove Fear, Aura of Courage; CHR-018): the [Fear] descriptor in data or in the description, or
    /// one of the PHB [Fear] spells of the levels in scope (Bane, Cause Fear, Doom, Fear, Phantasmal Killer, Scare). A
    /// spell that only suppresses or protects against fear (Calm Emotions, Remove Fear) is not a fear effect.
    /// </summary>
    internal static bool IsFearSpell(SpellData spell)
    {
        if (spell == null)
            return false;

        if (spell.HasDescriptor(SpellDescriptor.Fear))
            return true;

        string id = spell.SpellId;
        if (string.Equals(id, SpellNames.BANE, System.StringComparison.Ordinal)) return true;
        if (string.Equals(id, SpellNames.CAUSE_FEAR, System.StringComparison.Ordinal)) return true;
        if (string.Equals(id, SpellNames.DOOM, System.StringComparison.Ordinal)) return true;
        if (string.Equals(id, SpellNames.FEAR, System.StringComparison.Ordinal)) return true;
        if (string.Equals(id, SpellNames.PHANTASMAL_KILLER, System.StringComparison.Ordinal)) return true;
        if (string.Equals(id, SpellNames.SCARE, System.StringComparison.Ordinal)) return true;

        // The descriptor as written in the description ("Necromancy [Fear, Mind-Affecting]").
        return !string.IsNullOrWhiteSpace(spell.Description)
            && spell.Description.IndexOf("[Fear", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
