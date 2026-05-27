using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized service for managing spell effects, emanations, magic circles,
/// duration ticking, and effect expiration in D&amp;D 3.5e combat.
///
/// Owns the active emanation registry and provides all emanation/magic-circle
/// query helpers.  Also orchestrates per-round spell duration ticking, handles
/// effect expiration cleanup dispatch, and manages cleric spell level-specific
/// duration counters.
///
/// <para><b>Design:</b> Static utility class with internal state for the
/// emanation registry.  Call <see cref="ClearAll"/> on combat end.</para>
/// </summary>
public static class EffectService
{
    // ═══════════════════════════════════════════════════════════════════
    //  EMANATION REGISTRY
    // ═══════════════════════════════════════════════════════════════════

    private static readonly List<EmanationEffectData> _activeEmanations = new List<EmanationEffectData>();

    /// <summary>
    /// Register an emanation effect.  Replaces an existing emanation of the
    /// same concrete type centered on the same creature (one per type per creature).
    /// </summary>
    /// <param name="emanation">The emanation to register.</param>
    public static void RegisterEmanation(EmanationEffectData emanation)
    {
        if (emanation == null)
            return;

        // For mobile emanations, require a valid center creature
        if (!emanation.CenterPosition.HasValue && emanation.CenterCreature == null)
            return;

        // Remove any existing emanation of the same concrete type on the same center
        var emanationType = emanation.GetType();
        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            var existing = _activeEmanations[i];
            if (existing.GetType() == emanationType && existing.CenterCreature == emanation.CenterCreature)
            {
                _activeEmanations.RemoveAt(i);
            }
        }

        _activeEmanations.Add(emanation);
        Debug.Log($"[Emanation] {emanation.GetEffectName()} registered on {emanation.CenterCreature?.Stats?.CharacterName ?? "fixed position"}, CL {emanation.CasterLevel}, {emanation.RemainingRounds} rounds");
    }

    /// <summary>
    /// Unregister all emanations centered on a specific creature.
    /// Called on death, dispel, etc.
    /// </summary>
    /// <param name="centerCreature">The creature whose emanations should be removed.</param>
    public static void UnregisterEmanation(CharacterController centerCreature)
    {
        if (centerCreature == null) return;
        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            if (_activeEmanations[i].CenterCreature == centerCreature)
            {
                Debug.Log($"[Emanation] Removed {_activeEmanations[i].GetEffectName()} from {centerCreature.Stats?.CharacterName}");
                _activeEmanations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Tick all active emanations (call each round).  Removes expired or
    /// invalid ones.  Also refreshes Invisibility Sphere membership: creatures
    /// who have moved outside the emanation lose invisibility at round boundaries.
    /// </summary>
    /// <param name="refreshInvisibilitySpheres">
    /// Callback to refresh Invisibility Sphere membership before ticking.
    /// Typically delegates to <c>GameManager.RefreshInvisibilitySpheres()</c>.
    /// </param>
    public static void TickEmanations(Action refreshInvisibilitySpheres = null)
    {
        // Refresh Invisibility Sphere membership BEFORE ticking
        refreshInvisibilitySpheres?.Invoke();

        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            var em = _activeEmanations[i];
            if (em.ShouldRemove() || !em.Tick())
            {
                // For Invisibility Sphere, ensure all initially-affected creatures
                // are made visible before we drop the emanation.
                if (em is InvisibilitySphereEffect sphere)
                    sphere.EndForAll("duration expired");

                Debug.Log($"[Emanation] Expired: {em.GetEffectName()}");
                _activeEmanations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Get all active emanations (read-only copy for tests/queries).
    /// </summary>
    public static List<EmanationEffectData> GetActiveEmanations()
    {
        return new List<EmanationEffectData>(_activeEmanations);
    }

    /// <summary>
    /// Get all active emanations of a specific subclass type.
    /// </summary>
    /// <typeparam name="T">The emanation subclass type to filter by.</typeparam>
    /// <returns>List of active emanations of the requested type.</returns>
    public static List<T> GetActiveEmanationsOfType<T>() where T : EmanationEffectData
    {
        var result = new List<T>();
        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (_activeEmanations[i] is T typed)
                result.Add(typed);
        }
        return result;
    }

    /// <summary>
    /// Clear all tracked emanations.  Call on combat end.
    /// </summary>
    public static void ClearAll()
    {
        _activeEmanations.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MAGIC CIRCLE CONVENIENCE METHODS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register a Magic Circle emanation.  Convenience wrapper around
    /// <see cref="RegisterEmanation"/>.
    /// </summary>
    public static void RegisterMagicCircle(MagicCircleEffectData data)
    {
        RegisterEmanation(data);
    }

    /// <summary>
    /// Remove a Magic Circle effect (on death, dispel, etc.).
    /// Removes only <see cref="MagicCircleEffectData"/> emanations centered
    /// on the creature.
    /// </summary>
    public static void RemoveMagicCircle(CharacterController centerCreature)
    {
        if (centerCreature == null) return;
        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            if (_activeEmanations[i] is MagicCircleEffectData mc && mc.CenterCreature == centerCreature)
            {
                Debug.Log($"[MagicCircle] Removed {mc.GetSpellName()} from {centerCreature.Stats?.CharacterName}");
                _activeEmanations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Get Magic Circle protection benefits for a creature against an
    /// attacker's alignment.  Returns the best (highest) benefits found
    /// across all active circles covering the creature.
    /// </summary>
    public static AlignmentProtectionBenefits GetMagicCircleBenefitsAgainst(
        CharacterController creature, Alignment sourceAlignment)
    {
        var benefits = new AlignmentProtectionBenefits();
        if (creature == null || _activeEmanations.Count == 0)
            return benefits;

        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (!(_activeEmanations[i] is MagicCircleEffectData mc))
                continue;
            if (mc.CenterCreature == null || mc.CenterCreature.IsDead)
                continue;
            if (!mc.IsCreatureInArea(creature))
                continue;
            if (!mc.IsAttackerOfWardedAlignment(sourceAlignment))
                continue;

            benefits.HasMatch = true;
            benefits.DeflectionAcBonus = Mathf.Max(benefits.DeflectionAcBonus, 2);
            benefits.ResistanceSaveBonus = Mathf.Max(benefits.ResistanceSaveBonus, 2);
            benefits.BlocksMentalControl = true;
            benefits.BlocksSummonedContact = true;

            if (string.IsNullOrEmpty(benefits.SourceSpellName))
                benefits.SourceSpellName = mc.GetSpellName();
        }

        return benefits;
    }

    /// <summary>
    /// Check if a creature is within any active Magic Circle that protects
    /// against the given alignment.  Used for mental control suppression.
    /// </summary>
    public static bool IsProtectedByMagicCircle(
        CharacterController creature, AlignmentProtectionType wardedAlignment)
    {
        if (creature == null || _activeEmanations.Count == 0)
            return false;

        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (!(_activeEmanations[i] is MagicCircleEffectData mc))
                continue;
            if (mc.CenterCreature == null || mc.CenterCreature.IsDead)
                continue;
            if (mc.WardedAlignment != wardedAlignment)
                continue;
            if (mc.IsCreatureInArea(creature))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a creature is within any active Magic Circle.
    /// </summary>
    public static bool IsInAnyMagicCircle(CharacterController creature)
    {
        if (creature == null || _activeEmanations.Count == 0)
            return false;

        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (!(_activeEmanations[i] is MagicCircleEffectData mc))
                continue;
            if (mc.CenterCreature != null && !mc.CenterCreature.IsDead && mc.IsCreatureInArea(creature))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get all active Magic Circle effects (read-only access for tests/queries).
    /// </summary>
    public static List<MagicCircleEffectData> GetActiveMagicCircles()
    {
        return GetActiveEmanationsOfType<MagicCircleEffectData>();
    }

    /// <summary>
    /// Get the Magic Circle effect centered on a specific creature, or null.
    /// </summary>
    public static MagicCircleEffectData GetMagicCircleOnCreature(CharacterController creature)
    {
        if (creature == null) return null;
        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (_activeEmanations[i] is MagicCircleEffectData mc && mc.CenterCreature == creature)
                return mc;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CLERIC SPELL DURATION TICKING
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tick 2nd-level cleric spell durations for a single character.
    /// Handles Death Knell, Silence, and Align Weapon expiration.
    /// </summary>
    /// <param name="character">The character whose durations to tick.</param>
    /// <param name="logCallback">Optional combat log callback.</param>
    public static void TickClericSpell2Durations(
        CharacterController character, Action<string> logCallback = null)
    {
        if (character?.Stats == null) return;

        // ── Death Knell tick ──
        if (character.Stats.DeathKnellActive)
        {
            if (character.Stats.DeathKnellRoundsRemaining > 0)
            {
                character.Stats.DeathKnellRoundsRemaining--;
                if (character.Stats.DeathKnellRoundsRemaining <= 0)
                {
                    character.Stats.STR -= character.Stats.DeathKnellStrBonus;
                    character.Stats.DeathKnellActive = false;
                    character.Stats.DeathKnellStrBonus = 0;
                    character.Stats.DeathKnellCLBonus = 0;

                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "☠", character.Stats.CharacterName, "Death Knell buff"));
                    Debug.Log($"[DeathKnell] Buff expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Silence tick ──
        if (character.Stats.SilenceActive)
        {
            if (character.Stats.SilenceRoundsRemaining > 0)
            {
                character.Stats.SilenceRoundsRemaining--;
                if (character.Stats.SilenceRoundsRemaining <= 0)
                {
                    character.Stats.SilenceActive = false;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "🔇", character.Stats.CharacterName, "Silence"));
                    Debug.Log($"[Silence] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Align Weapon tick ──
        if (character.Stats.AlignWeaponActive)
        {
            if (character.Stats.AlignWeaponRoundsRemaining > 0)
            {
                character.Stats.AlignWeaponRoundsRemaining--;
                if (character.Stats.AlignWeaponRoundsRemaining <= 0)
                {
                    character.Stats.AlignWeaponActive = false;
                    character.Stats.AlignWeaponAlignment = null;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "⚔", character.Stats.CharacterName, "Align Weapon"));
                    Debug.Log($"[AlignWeapon] Expired on {character.Stats.CharacterName}");
                }
            }
        }
    }

    /// <summary>
    /// Tick 3rd-level cleric spell durations for a single character.
    /// Handles Prayer and Invisibility Purge expiration.
    /// </summary>
    /// <param name="character">The character whose durations to tick.</param>
    /// <param name="logCallback">Optional combat log callback.</param>
    public static void TickClericSpell3Durations(
        CharacterController character, Action<string> logCallback = null)
    {
        if (character?.Stats == null) return;

        // ── Prayer tick ──
        if (character.Stats.PrayerActive)
        {
            if (character.Stats.PrayerRoundsRemaining > 0)
            {
                character.Stats.PrayerRoundsRemaining--;
                if (character.Stats.PrayerRoundsRemaining <= 0)
                {
                    character.Stats.PrayerActive = false;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "🙏", character.Stats.CharacterName, "Prayer effect"));
                    Debug.Log($"[Prayer] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Invisibility Purge tick ──
        if (character.Stats.InvisibilityPurgeActive)
        {
            if (character.Stats.InvisibilityPurgeRoundsRemaining > 0)
            {
                character.Stats.InvisibilityPurgeRoundsRemaining--;
                if (character.Stats.InvisibilityPurgeRoundsRemaining <= 0)
                {
                    character.Stats.InvisibilityPurgeActive = false;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "👁", character.Stats.CharacterName, "Invisibility Purge"));
                    Debug.Log($"[InvisibilityPurge] Expired on {character.Stats.CharacterName}");
                }
            }
        }
    }

    /// <summary>
    /// Tick 4th-level cleric spell durations for a single character.
    /// Handles Death Ward, Divine Power, Freedom of Movement, Spell Immunity,
    /// Repel Vermin, and Neutralize Poison Immunity expiration.
    /// </summary>
    /// <param name="character">The character whose durations to tick.</param>
    /// <param name="logCallback">Optional combat log callback.</param>
    public static void TickClericSpell4Durations(
        CharacterController character, Action<string> logCallback = null)
    {
        if (character?.Stats == null) return;

        // ── Death Ward ──
        if (character.Stats.DeathWardActive)
        {
            if (character.Stats.DeathWardRoundsRemaining > 0)
            {
                character.Stats.DeathWardRoundsRemaining--;
                if (character.Stats.DeathWardRoundsRemaining <= 0)
                {
                    character.Stats.DeathWardActive = false;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "🛡", character.Stats.CharacterName, "Death Ward"));
                    Debug.Log($"[DeathWard] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Divine Power ──
        if (character.Stats.DivinePowerActive)
        {
            if (character.Stats.DivinePowerRoundsRemaining > 0)
            {
                character.Stats.DivinePowerRoundsRemaining--;
                if (character.Stats.DivinePowerRoundsRemaining <= 0)
                {
                    character.Stats.STR -= character.Stats.DivinePowerStrBonus;
                    character.Stats.TempHP = Mathf.Max(0, character.Stats.TempHP - character.Stats.DivinePowerTempHP);
                    character.Stats.BaseAttackBonus -= character.Stats.DivinePowerBABBonus;

                    character.Stats.DivinePowerActive = false;
                    character.Stats.DivinePowerStrBonus = 0;
                    character.Stats.DivinePowerTempHP = 0;
                    character.Stats.DivinePowerBABBonus = 0;

                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "⚔", character.Stats.CharacterName, "Divine Power"));
                    Debug.Log($"[DivinePower] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Freedom of Movement ──
        if (character.Stats.FreedomOfMovementActive)
        {
            if (character.Stats.FreedomOfMovementRoundsRemaining > 0)
            {
                character.Stats.FreedomOfMovementRoundsRemaining--;
                if (character.Stats.FreedomOfMovementRoundsRemaining <= 0)
                {
                    character.Stats.FreedomOfMovementActive = false;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "🦅", character.Stats.CharacterName, "Freedom of Movement"));
                    Debug.Log($"[FreedomOfMovement] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Spell Immunity ──
        if (character.Stats.SpellImmunityActive)
        {
            if (character.Stats.SpellImmunityRoundsRemaining > 0)
            {
                character.Stats.SpellImmunityRoundsRemaining--;
                if (character.Stats.SpellImmunityRoundsRemaining <= 0)
                {
                    character.Stats.SpellImmunityActive = false;
                    character.Stats.SpellImmunitySpellId = null;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "🛡🔮", character.Stats.CharacterName, "Spell Immunity"));
                    Debug.Log($"[SpellImmunity] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Repel Vermin ──
        if (character.Stats.RepelVerminActive)
        {
            if (character.Stats.RepelVerminRoundsRemaining > 0)
            {
                character.Stats.RepelVerminRoundsRemaining--;
                if (character.Stats.RepelVerminRoundsRemaining <= 0)
                {
                    character.Stats.RepelVerminActive = false;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "🐛", character.Stats.CharacterName, "Repel Vermin"));
                    Debug.Log($"[RepelVermin] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Neutralize Poison Immunity ──
        if (character.Stats.NeutralizePoisonImmunityActive)
        {
            if (character.Stats.NeutralizePoisonImmunityRoundsRemaining > 0)
            {
                character.Stats.NeutralizePoisonImmunityRoundsRemaining--;
                if (character.Stats.NeutralizePoisonImmunityRoundsRemaining <= 0)
                {
                    character.Stats.NeutralizePoisonImmunityActive = false;
                    logCallback?.Invoke(CombatLogHelper.ConditionFaded(
                        "🌿", character.Stats.CharacterName, "poison immunity"));
                    Debug.Log($"[NeutralizePoison] Immunity expired on {character.Stats.CharacterName}");
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ENERGY RESISTANCE / PROTECTION DURATION TICKING
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tick all Resist Energy effects on a character, removing expired ones.
    /// </summary>
    /// <param name="character">The character whose resist energy effects to tick.</param>
    /// <param name="logCallback">Optional combat log callback.</param>
    public static void TickResistEnergyEffects(
        CharacterController character, Action<string> logCallback = null)
    {
        if (character?.Stats == null) return;
        var effects = character.Stats.ActiveResistEnergyEffects;
        if (effects == null || effects.Count == 0) return;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            ResistEnergyEffectData effect = effects[i];
            if (effect == null)
            {
                effects.RemoveAt(i);
                continue;
            }

            if (effect.DurationRemainingRounds >= 0)
                effect.DurationRemainingRounds--;

            if (effect.DurationRemainingRounds <= 0)
            {
                string energyLabel = DamageTextUtils.GetDamageTypeDisplay(effect.ToDamageType());
                logCallback?.Invoke(CombatLogHelper.Expired(
                    "⏱", $"Resist Energy ({energyLabel}) expires on {character.Stats.CharacterName}."));
                effects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Tick all Protection from Energy effects on a character, removing expired ones.
    /// </summary>
    /// <param name="character">The character whose protection effects to tick.</param>
    /// <param name="logCallback">Optional combat log callback.</param>
    public static void TickProtectionFromEnergyEffects(
        CharacterController character, Action<string> logCallback = null)
    {
        if (character?.Stats == null) return;
        var effects = character.Stats.ActiveProtectionFromEnergyEffects;
        if (effects == null || effects.Count == 0) return;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            ProtectionFromEnergyEffectData protEffect = effects[i];
            if (protEffect == null)
            {
                effects.RemoveAt(i);
                continue;
            }

            if (protEffect.DurationRemainingRounds >= 0)
                protEffect.DurationRemainingRounds--;

            if (protEffect.DurationRemainingRounds <= 0)
            {
                string protEnergyLabel = protEffect.GetDisplayLabel();
                logCallback?.Invoke(CombatLogHelper.Expired(
                    "⏱", $"Protection from Energy ({protEnergyLabel}) expires on {character.Stats.CharacterName}."));
                effects.RemoveAt(i);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ENFEEBLEMENT / TOUCH OF IDIOCY DURATION TICKING
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tick enfeeblement (Ray of Enfeeblement) and Touch of Idiocy debuffs
    /// on a character, logging expiration if applicable.
    /// </summary>
    /// <param name="character">The character whose debuffs to tick.</param>
    /// <param name="logCallback">Optional combat log callback.</param>
    public static void TickDebuffEffects(
        CharacterController character, Action<string> logCallback = null)
    {
        if (character == null) return;

        EnfeebledConditionData expiredEnfeeblement = character.TickEnfeeblementEffect();
        if (expiredEnfeeblement != null)
        {
            int amount = Mathf.Max(0, expiredEnfeeblement.StrengthPenaltyAmount);
            string sourceName = !string.IsNullOrWhiteSpace(expiredEnfeeblement.CasterName)
                ? expiredEnfeeblement.CasterName
                : "an unknown caster";
            logCallback?.Invoke(CombatLogHelper.Expired(
                "⏱", $"Ray of Enfeeblement expires on {character.Stats.CharacterName}: STR +{amount} restored (source: {sourceName})."));
        }

        TouchOfIdiocyConditionData expiredIdiocy = character.TickTouchOfIdiocyEffect();
        if (expiredIdiocy != null)
        {
            logCallback?.Invoke(
                $"<color=#FFAA44>⏱ Touch of Idiocy expires on {character.Stats.CharacterName}: " +
                $"INT +{Mathf.Max(0, expiredIdiocy.IntelligenceDamage)}, " +
                $"WIS +{Mathf.Max(0, expiredIdiocy.WisdomDamage)}, " +
                $"CHA +{Mathf.Max(0, expiredIdiocy.CharismaDamage)} restored.</color>");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DAILY EFFECTS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply once-per-day disease progression and natural ability damage recovery.
    /// Can be called by rest/day systems; also auto-called every 14,400 rounds.
    /// </summary>
    /// <param name="allCharacters">All living characters to process.</param>
    public static void ProcessDailyEffects(List<CharacterController> allCharacters)
    {
        if (allCharacters == null || allCharacters.Count == 0)
            return;

        foreach (CharacterController character in allCharacters)
        {
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            character.ProcessDiseaseEffectsDaily();
            character.HealAbilityDamageDaily(1, "Daily recovery");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WEB ENTANGLED CONDITION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply a Web Entangled condition to a target character.
    /// </summary>
    /// <param name="caster">The caster who created the web.</param>
    /// <param name="target">The target to entangle.</param>
    /// <param name="durationRounds">Duration in rounds.</param>
    /// <param name="conditionService">The condition service instance (may be null).</param>
    public static void ApplyWebEntangledCondition(
        CharacterController caster,
        CharacterController target,
        int durationRounds,
        ConditionService conditionService)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead)
            return;

        var data = new WebEntangledConditionData
        {
            Caster = caster,
            Target = target,
            EscapeDC = WebAreaEffect.EscapeDc,
            SourceSpellId = SpellNames.WEB,
            SourceSpellName = "Web"
        };

        int rounds = Mathf.Max(1, durationRounds);
        if (conditionService != null)
        {
            conditionService.ApplyCondition(
                target,
                CombatConditionType.Entangled,
                rounds,
                source: caster,
                data: data,
                sourceNameOverride: "Web",
                sourceCategory: "Spell",
                sourceId: SpellNames.WEB);
        }
        else
        {
            target.ApplyCondition(CombatConditionType.Entangled, rounds, "Web");
        }
    }

    /// <summary>
    /// Remove Web Entangled conditions when a web area expires.
    /// Only removes the condition if no other active web area still covers the creature.
    /// </summary>
    /// <param name="sourceArea">The web area that is being removed.</param>
    /// <param name="allCharacters">All characters to check.</param>
    /// <param name="tryGetWebCondition">
    /// Delegate to check if a character has a web entangled condition.
    /// </param>
    /// <param name="logCallback">Optional combat log callback.</param>
    public static void RemoveWebEntangledConditionsFromArea(
        WebAreaEffect sourceArea,
        List<CharacterController> allCharacters,
        Func<CharacterController, bool> tryGetWebCondition,
        Action<string> logCallback = null)
    {
        if (sourceArea == null || allCharacters == null)
            return;

        List<WebAreaEffect> activeWebs = AreaEffectManager.Instance.GetEffectsOfType<WebAreaEffect>();

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController character = allCharacters[i];
            if (character == null || character.Stats == null)
                continue;

            if (tryGetWebCondition != null && !tryGetWebCondition(character))
                continue;

            // Only clear if no remaining web area still covers the creature.
            bool stillCoveredByAnyWeb = false;
            for (int j = 0; j < activeWebs.Count; j++)
            {
                WebAreaEffect web = activeWebs[j];
                if (web == null || web == sourceArea)
                    continue;
                if (web.IsCellInArea(character.GridPosition))
                {
                    stillCoveredByAnyWeb = true;
                    break;
                }
            }

            if (stillCoveredByAnyWeb)
                continue;

            character.RemoveCondition(CombatConditionType.Entangled);
            logCallback?.Invoke($"🕸 {character.Stats.CharacterName} is freed as the web dissipates.");
        }
    }
}
