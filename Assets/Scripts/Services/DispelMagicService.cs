using System;
using System.Collections.Generic;
using System.Linq;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;

// ============================================================================
// DispelMagicService — Centralized Dispel Magic & Counterspell system.
//
// Extracted from GameManager.DispelCounterspell.cs to decouple dispel/counterspell
// logic from the GameManager God Object.
//
// Contains:
//   PURE STATIC section — no state required:
//     PerformDispelCheck, RollDispelCheck, GetDispelDC, PerformCounterspellDispelCheck
//
//   INSTANCE section — requires Initialize() with dependency injection:
//     PerformTargetedDispel, PerformAreaDispel, DispelSingleEffect,
//     HandleDispelSpecialCleanup, TryResolveCounterspell,
//     ResolveSameSpellCounterspell, ResolveDesignatedCounterspell,
//     ResolveDispelMagicCounterspell, ExpireReadiedCounterspell
//
// D&D 3.5e PHB p.223: Dispel Magic rules and Counterspell flow.
//
// PATTERN:
//   1. MonoBehaviour attached to GameManager GameObject.
//   2. Initialize() called by GameManager after Awake().
//   3. CombatUI accessed via injected Func<CombatUI> provider.
//   4. GameManager retains thin delegate wrappers for backward compatibility.
// ============================================================================

/// <summary>
/// Service for all Dispel Magic and Counterspell mechanics.
/// Handles targeted dispel, area dispel, counterspell resolution, and special
/// cleanup when specific spell effects are removed.
/// </summary>
public class DispelMagicService : MonoBehaviour
{
    // ==================== INJECTED DEPENDENCIES ====================

    private Func<CombatUI> _combatUIProvider;
    private Func<List<CharacterController>> _getAllCharacters;
    private Action _updateAllStatsUI;
    private Action<CharacterController> _clearResilientSphereState;
    private Action<CharacterController> _handleSummonDeathCleanup;

    private CombatUI CombatUI => _combatUIProvider?.Invoke();

    // ==================== LIFECYCLE ====================

    /// <summary>
    /// Called by GameManager after Awake() to inject dependencies.
    /// </summary>
    /// <param name="combatUIProvider">Provides the active CombatUI reference.</param>
    /// <param name="getAllCharacters">Returns all active combatants.</param>
    /// <param name="updateAllStatsUI">Callback to refresh all stat UIs after dispel.</param>
    /// <param name="clearResilientSphereState">Callback to clear Resilient Sphere state.</param>
    /// <param name="handleSummonDeathCleanup">Callback to handle summon death cleanup.</param>
    public void Initialize(
        Func<CombatUI> combatUIProvider,
        Func<List<CharacterController>> getAllCharacters,
        Action updateAllStatsUI,
        Action<CharacterController> clearResilientSphereState,
        Action<CharacterController> handleSummonDeathCleanup)
    {
        _combatUIProvider = combatUIProvider;
        _getAllCharacters = getAllCharacters;
        _updateAllStatsUI = updateAllStatsUI;
        _clearResilientSphereState = clearResilientSphereState;
        _handleSummonDeathCleanup = handleSummonDeathCleanup;
        Debug.Log("[DispelMagicService] Initialized");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PURE STATIC — Dispel Check Formulas (D&D 3.5e PHB p.223)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Perform the D&D 3.5e dispel check.
    /// Formula: 1d20 + min(casterLevel, 10) vs DC 11 + targetSpellCasterLevel.
    /// Auto-succeeds against own spells.
    /// Returns true if the check succeeds.
    /// </summary>
    public static bool PerformDispelCheck(int casterLevel, int targetSpellCasterLevel, bool isOwnSpell)
    {
        if (isOwnSpell) return true;

        int clampedCL = Mathf.Min(casterLevel, 10);
        int roll = DiceService.D20("Counterspell dispel check"); // 1d20
        int total = roll + clampedCL;
        int dc = 11 + targetSpellCasterLevel;

        Debug.Log($"[DispelMagic] Dispel check: 1d20({roll}) + CL({clampedCL}) = {total} vs DC {dc} (11 + {targetSpellCasterLevel})");
        return total >= dc;
    }

    /// <summary>
    /// Perform the D&D 3.5e dispel check and return the total roll (for comparing against multiple DCs).
    /// Formula: 1d20 + min(casterLevel, 10).
    /// </summary>
    public static int RollDispelCheck(int casterLevel)
    {
        int clampedCL = Mathf.Min(casterLevel, 10);
        int roll = DiceService.D20("Counterspell Dispel Magic check"); // 1d20
        int total = roll + clampedCL;
        Debug.Log($"[DispelMagic] Dispel roll: 1d20({roll}) + CL({clampedCL}) = {total}");
        return total;
    }

    /// <summary>
    /// Calculate the DC to dispel a spell effect.
    /// DC = 11 + target spell's caster level.
    /// </summary>
    public static int GetDispelDC(int targetSpellCasterLevel)
    {
        return 11 + targetSpellCasterLevel;
    }

    /// <summary>
    /// Static helper: Perform a counterspell dispel check (for testing).
    /// Same formula as PerformDispelCheck but specifically for counterspell context.
    /// 1d20 + CL (max +10 for Dispel, +20 for Greater Dispel) vs DC 11 + enemy CL.
    /// </summary>
    public static bool PerformCounterspellDispelCheck(int counterCL, int enemyCL, int maxCLBonus = 10)
    {
        int clCapped = Mathf.Min(counterCL, maxCLBonus);
        int roll = DiceService.D20("NPC Dispel Magic counterspell check");
        int total = roll + clCapped;
        int dc = 11 + enemyCL;
        bool success = total >= dc;
        Debug.Log($"[Counterspell] Dispel check: d20({roll}) + CL({clCapped}) = {total} vs DC {dc} (11 + {enemyCL}) → {(success ? "SUCCESS" : "FAIL")}");
        return success;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TARGETED DISPEL — D&D 3.5e PHB p.223
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Perform a targeted dispel on a single creature.
    /// D&D 3.5e PHB p.223:
    /// 1. Roll one dispel check: 1d20 + CL (max +10)
    /// 2. Compare against spells in descending CL order (highest first)
    /// 3. Auto-succeed against own spells
    /// 4. Remove at most ONE spell
    /// 5. Handle special cleanup for specific spells (Bear's Endurance, Spectral Hand, etc.)
    /// </summary>
    public void PerformTargetedDispel(CharacterController caster, CharacterController target)
    {
        if (caster == null || target == null || target.Stats == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Dispel Magic: Invalid target."));
            return;
        }

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        string targetName = target.Stats.CharacterName;
        int casterLevel = caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;

        StatusEffectManager targetStatusMgr = target.StatusEffectManager;
        if (targetStatusMgr == null || targetStatusMgr.ActiveEffects == null || targetStatusMgr.ActiveEffects.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellResistance("🔮", $"{casterName} casts Dispel Magic on {targetName} — no active spell effects to dispel."));
            Debug.Log($"[DispelMagic] {casterName} targets {targetName} — no active effects found");
            return;
        }

        // Get list of dispellable effects sorted by caster level (descending), then by spell level (descending)
        var dispellableEffects = new List<ActiveSpellEffect>();
        foreach (var effect in targetStatusMgr.ActiveEffects)
        {
            if (effect == null || effect.Spell == null)
                continue;
            // Cannot dispel instantaneous effects (they already happened)
            if (effect.Spell.DurationType == DurationType.Instantaneous)
                continue;
            dispellableEffects.Add(effect);
        }

        if (dispellableEffects.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellResistance("🔮", $"{casterName} casts Dispel Magic on {targetName} — no dispellable effects found."));
            Debug.Log($"[DispelMagic] {casterName} targets {targetName} — all effects are instantaneous or non-dispellable");
            return;
        }

        // Sort by caster level descending (highest first), then by remaining rounds descending as tiebreaker
        dispellableEffects.Sort((a, b) =>
        {
            int clCompare = b.CasterLevel.CompareTo(a.CasterLevel);
            if (clCompare != 0) return clCompare;
            return b.RemainingRounds.CompareTo(a.RemainingRounds);
        });

        // Roll once: 1d20 + min(CL, 10)
        int dispelRoll = RollDispelCheck(casterLevel);

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellResistance("🔮", $"{casterName} casts Dispel Magic on {targetName} (dispel check: {dispelRoll})"));

        bool dispelledSomething = false;
        foreach (var effect in dispellableEffects)
        {
            bool isOwnSpell = !string.IsNullOrEmpty(effect.CasterName) &&
                              string.Equals(effect.CasterName, casterName, StringComparison.OrdinalIgnoreCase);

            if (isOwnSpell)
            {
                // Auto-success against own spells
                DispelSingleEffect(target, targetStatusMgr, effect, casterName, "(auto-success, own spell)");
                dispelledSomething = true;
                break;
            }

            int dc = GetDispelDC(effect.CasterLevel);
            if (dispelRoll >= dc)
            {
                DispelSingleEffect(target, targetStatusMgr, effect, casterName, $"(roll {dispelRoll} ≥ DC {dc})");
                dispelledSomething = true;
                break;
            }
            else
            {
                Debug.Log($"[DispelMagic] Failed to dispel {effect.Spell.Name} (CL {effect.CasterLevel}): " +
                          $"roll {dispelRoll} < DC {dc}");
            }
        }

        if (!dispelledSomething)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("❌", $"Dispel Magic fails — could not overcome any spell on {targetName}."));
            Debug.Log($"[DispelMagic] All dispel checks failed on {targetName}");
        }

        _updateAllStatsUI?.Invoke();
    }

    /// <summary>
    /// Perform an area dispel affecting all characters within range.
    /// D&D 3.5e PHB p.223: 20-ft radius burst.
    /// Simplified: targets all characters in combat (within range).
    /// Each creature gets a separate targeted dispel (max 1 spell removed per creature).
    /// Magic items are NOT affected by area dispel.
    /// </summary>
    public void PerformAreaDispel(CharacterController caster, List<CharacterController> targets)
    {
        if (caster == null || targets == null || targets.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "Dispel Magic (area): No targets in range."));
            return;
        }

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        CombatUI?.ShowCombatLog(CombatLogHelper.SpellResistance("🔮", $"{casterName} casts Dispel Magic (area dispel) — targeting {targets.Count} creature(s)"));

        foreach (var target in targets)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            PerformTargetedDispel(caster, target);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DISPEL EFFECT — Single effect removal + special cleanup
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Remove a single spell effect from a target and handle special cleanup.
    /// Called when a dispel check succeeds against a specific effect.
    /// </summary>
    private void DispelSingleEffect(CharacterController target, StatusEffectManager statusMgr, ActiveSpellEffect effect, string casterName, string checkDetail)
    {
        if (effect == null || effect.Spell == null) return;

        string spellName = effect.Spell.Name;
        string spellId = effect.Spell.SpellId;
        string targetName = target.Stats != null ? target.Stats.CharacterName : "Unknown";

        CombatUI?.ShowCombatLog(CombatLogHelper.Success("✅", $"Dispel Magic succeeds — {spellName} dispelled from {targetName} {checkDetail}"));
        Debug.Log($"[DispelMagic] Dispelled {spellName} (CL {effect.CasterLevel}) from {targetName} {checkDetail}");

        // Handle special effect cleanup before removing the tracked effect
        HandleDispelSpecialCleanup(target, spellId, effect);

        // Remove the effect from StatusEffectManager (handles stat reversal)
        statusMgr.RemoveEffect(effect);
    }

    /// <summary>
    /// Handle special cleanup when specific spells are dispelled.
    /// Some spells have side effects on removal (e.g., Bear's Endurance can kill from HP loss).
    /// </summary>
    private void HandleDispelSpecialCleanup(CharacterController target, string spellId, ActiveSpellEffect effect)
    {
        if (target == null || string.IsNullOrEmpty(spellId)) return;

        // --- Bear's Endurance / attribute enhancements ---
        // Removing CON enhancement reduces max HP, which can kill the target
        if (spellId == SpellNames.BEARS_ENDURANCE || spellId == SpellNames.BULLS_STRENGTH ||
            spellId == SpellNames.CATS_GRACE || spellId == SpellNames.EAGLES_SPLENDOR ||
            spellId == SpellNames.FOXS_CUNNING || spellId == SpellNames.OWLS_WISDOM)
        {
            // The attribute enhancement system handles HP adjustments on removal
            string abilityName = "";
            AbilityType ability = AbilityType.STR;
            switch (spellId)
            {
                case SpellNames.BEARS_ENDURANCE: ability = AbilityType.CON; abilityName = "CON"; break;
                case SpellNames.BULLS_STRENGTH: ability = AbilityType.STR; abilityName = "STR"; break;
                case SpellNames.CATS_GRACE: ability = AbilityType.DEX; abilityName = "DEX"; break;
                case SpellNames.EAGLES_SPLENDOR: ability = AbilityType.CHA; abilityName = "CHA"; break;
                case SpellNames.FOXS_CUNNING: ability = AbilityType.INT; abilityName = "INT"; break;
                case SpellNames.OWLS_WISDOM: ability = AbilityType.WIS; abilityName = "WIS"; break;
            }

            bool causedDeath = target.RemoveAttributeEnhancement(ability);
            if (causedDeath && target.Stats != null)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("💀", $"Dispelling {effect.Spell.Name} killed {target.Stats.CharacterName}! (HP loss from {abilityName} reduction)"));
                target.OnDeath();
                _handleSummonDeathCleanup?.Invoke(target);
            }
            return;
        }

        // --- Spectral Hand ---
        // Dispelling Spectral Hand: caster regains HP (hand wasn't destroyed, it was dispelled)
        if (spellId == SpellNames.SPECTRAL_HAND)
        {
            target.RemoveSpectralHandEffect(); // This restores HP to caster
            Debug.Log($"[DispelMagic] Spectral Hand dispelled — caster regains lost HP");
            return;
        }

        // --- Invisibility ---
        if (spellId == SpellNames.INVISIBILITY)
        {
            target.ForceEndInvisibility("dispelled");
            return;
        }

        // --- See Invisibility ---
        if (spellId == SpellNames.SEE_INVISIBLE || spellId == SpellNames.SEE_INVISIBILITY_LEGACY)
        {
            target.ClearSeeInvisibilityEffect();
            return;
        }

        // --- Command Undead ---
        if (spellId == SpellNames.COMMAND_UNDEAD)
        {
            if (target.ActiveCommandUndeadEffect != null)
            {
                target.RemoveCommandUndeadEffect();
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("💀", $"Command Undead control broken on {target.Stats?.CharacterName}!"));
            }
            return;
        }

        // --- False Life ---
        if (spellId == SpellNames.FALSE_LIFE)
        {
            target.RemoveFalseLifeEffect();
            return;
        }

        // --- Disguise Self ---
        if (spellId == SpellNames.DISGUISE_SELF)
        {
            target.ClearDisguiseSelfEffect();
            return;
        }

        // --- Expeditious Retreat ---
        if (spellId == SpellNames.EXPEDITIOUS_RETREAT)
        {
            target.ClearExpeditiousRetreatEffect();
            return;
        }

        // --- Haste ---
        if (spellId == SpellNames.HASTE)
        {
            target.ClearHasteEffect();
            if (target.Stats != null)
            {
                target.Stats.HasteAttackBonus = 0;
                target.Stats.HasteACBonus = 0;
                target.Stats.HasteReflexBonus = 0;
            }
            return;
        }

        // --- Slow ---
        if (spellId == SpellNames.SLOW)
        {
            target.ClearSlowEffect();
            if (target.Stats != null)
            {
                target.Stats.SlowAttackPenalty = 0;
                target.Stats.SlowACPenalty = 0;
                target.Stats.SlowReflexPenalty = 0;
                target.Stats.SlowSpeedMultiplier = 1f;
            }
            return;
        }

        // --- Fire Shield ---
        if (spellId == SpellNames.FIRE_SHIELD)
        {
            if (target.Stats != null)
            {
                target.Stats.FireShieldActive = false;
                target.Stats.FireShieldIsWarm = false;
                target.Stats.FireShieldCasterLevel = 0;
                target.Stats.FireShieldDurationRounds = 0;
            }
            return;
        }

        // --- Resilient Sphere (PHB p.263) ---
        // Now handled as area effect. If dispel target is inside a sphere, remove that sphere.
        if (spellId == SpellNames.RESILIENT_SPHERE)
        {
            _clearResilientSphereState?.Invoke(target);
            return;
        }

        // --- Blur ---
        // Blur is handled by StatusEffectManager.RemoveEffect() reversing the concealment stats.
        // No dedicated cleanup method needed.

        // --- Blindness/Deafness ---
        if (spellId == SpellNames.BLINDNESS_DEAFNESS)
        {
            target.RemoveBlindnessDeafnessEffect();
            return;
        }

        // --- Imbue with Spell Ability ---
        if (spellId == SpellNames.IMBUE_WITH_SPELL_ABILITY)
        {
            // If dispelled from the caster, end the entire imbue link
            if (target.Stats.ImbueWithSpellAbilityCasterActive)
            {
                ImbueWithSpellAbilityManager.EndImbueEffect(target, target.Stats.ImbueTarget, "dispelled from caster");
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("✨", $"Imbue with Spell Ability dispelled — {target.Stats?.CharacterName}'s locked spell slots freed."));
            }
            // If dispelled from the target, end the entire imbue link
            else if (target.Stats.ImbueWithSpellAbilityTargetActive)
            {
                ImbueWithSpellAbilityManager.EndImbueEffect(target.Stats.ImbueCaster, target, "dispelled from target");
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("✨", "Imbue with Spell Ability dispelled — imbued spells lost."));
            }
            return;
        }

        // For other spells, StatusEffectManager.RemoveEffect() handles the stat reversal
    }

    // ═══════════════════════════════════════════════════════════════════
    //  COUNTERSPELL SYSTEM — D&D 3.5e PHB Counterspell Flow
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check all characters for readied counterspells that should trigger when a given caster
    /// begins casting a spell. Returns the result of the first successful counterspell attempt,
    /// or null if no counterspell was attempted or all failed.
    /// 
    /// D&D 3.5e PHB Counterspell Flow:
    /// 1. Caster begins casting → triggers readied counterspell actions
    /// 2. Counterspeller identifies spell via Spellcraft (DC 15 + spell level)
    /// 3. Counterspeller uses same spell (auto-success) or Dispel Magic (dispel check)
    /// 4. Both sides expend spell slots regardless of outcome
    /// </summary>
    /// <param name="caster">The character casting the spell.</param>
    /// <param name="spell">The spell being cast.</param>
    /// <param name="isSpellLikeAbility">True if this is an SLA (cannot be counterspelled).</param>
    /// <returns>CounterspellResult if a counterspell was attempted, null otherwise.</returns>
    public CounterspellResult TryResolveCounterspell(CharacterController caster, SpellData spell, bool isSpellLikeAbility = false)
    {
        if (caster == null || spell == null) return null;

        // SLAs, supernatural, and extraordinary abilities cannot be counterspelled (PHB)
        if (isSpellLikeAbility)
        {
            Debug.Log($"[Counterspell] {spell.Name} is a spell-like ability — cannot be counterspelled.");
            return null;
        }

        // Find all characters with active counterspells that should trigger
        List<CharacterController> allChars = _getAllCharacters?.Invoke();
        if (allChars == null) return null;

        CounterspellData triggeringCounterspell = null;
        CharacterController counterspeller = null;

        foreach (var c in allChars)
        {
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (!c.HasReadiedCounterspell) continue;
            if (!c.ReadiedCounterspell.ShouldTriggerFor(caster)) continue;

            // Check range: counterspeller must be able to see the caster
            // Simplified: check if within a reasonable distance (120 ft / 24 squares for most spells)
            int distance = SquareGridUtils.GetDistance(c.GridPosition, caster.GridPosition);

            // For range check, we use the maximum of:
            // - The range of any spell the counterspeller could use to counter
            // - Medium range (100 ft + 10 ft/level) for Dispel Magic
            int casterLevel = Mathf.Max(1, c.Stats.GetCasterLevel());
            int dispelRange = (100 + 10 * casterLevel) / 5; // Convert feet to squares (5ft per square)

            if (distance > dispelRange && distance > 24) // Rough max range check
            {
                Debug.Log($"[Counterspell] {c.Stats.CharacterName}: Out of range to counterspell {caster.Stats.CharacterName} (distance {distance * 5}ft).");
                continue;
            }

            triggeringCounterspell = c.ReadiedCounterspell;
            counterspeller = c;
            break; // Only one counterspell triggers at a time
        }

        if (triggeringCounterspell == null || counterspeller == null)
            return null;

        // Mark the readied action as triggered
        triggeringCounterspell.MarkTriggered();

        string casterName = caster.Stats.CharacterName;
        string counterName = counterspeller.Stats.CharacterName;

        CombatUI?.ShowCombatLog(CombatLogHelper.Special("⚡", $"{counterName}'s readied counterspell triggers against {casterName}!"));
        Debug.Log($"[Counterspell] {counterName}'s readied counterspell triggers! {casterName} is casting {spell.Name}.");

        // Step 1: Try to identify the spell via Spellcraft (free action)
        bool spellIdentified = counterspeller.RollSpellcraftIdentification(
            spell.SpellLevel, out int scRoll, out int scTotal, out int scDC);

        CounterspellResult result = new CounterspellResult
        {
            Counterspeller = counterspeller,
            OriginalCaster = caster,
            EnemySpell = spell,
            SpellIdentified = spellIdentified,
            SpellcraftRoll = scTotal,
            SpellcraftDC = scDC
        };

        if (spellIdentified)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"🔍 {counterName} identifies {spell.Name}! (Spellcraft {scTotal} vs DC {scDC})"));
        }
        else
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", $"{counterName} fails to identify the spell. (Spellcraft {scTotal} vs DC {scDC})"));
        }

        // Step 2: Try same-spell counter (requires identification)
        if (spellIdentified)
        {
            // Check for designated counter pairs first (Haste/Slow, Bless/Bane, etc.)
            string designatedCounter = DesignatedCounterPairs.GetDesignatedCounter(spell.SpellId);
            if (designatedCounter != null && counterspeller.HasSpellAvailableForCounter(designatedCounter))
            {
                return ResolveDesignatedCounterspell(counterspeller, caster, spell, designatedCounter, result);
            }

            // Check for same spell
            if (counterspeller.HasSpellAvailableForCounter(spell.SpellId))
            {
                return ResolveSameSpellCounterspell(counterspeller, caster, spell, result);
            }
        }

        // Step 3: Fall back to Dispel Magic (doesn't require identification)
        if (counterspeller.HasDispelMagicAvailable())
        {
            return ResolveDispelMagicCounterspell(counterspeller, caster, spell, result);
        }

        // No counter method available
        result.Success = false;
        result.Method = "None";
        result.LogMessage = $"⚠ {counterName} cannot counter {spell.Name} — no suitable spell available.";
        CombatUI?.ShowCombatLog(result.LogMessage);
        Debug.Log($"[Counterspell] {counterName}: No counter method available for {spell.Name}.");

        // Clear the readied counterspell since it was triggered but couldn't be used
        counterspeller.ClearReadiedCounterspell();

        return result;
    }

    /// <summary>
    /// Resolve a same-spell counterspell attempt.
    /// PHB: Automatic success, no check needed. Both spells negate each other.
    /// </summary>
    private CounterspellResult ResolveSameSpellCounterspell(
        CharacterController counterspeller, CharacterController caster,
        SpellData enemySpell, CounterspellResult result)
    {
        string counterName = counterspeller.Stats.CharacterName;
        string casterName = caster.Stats.CharacterName;

        // Consume counterspeller's spell slot
        bool consumed = counterspeller.ConsumeSpellSlotForCounter(enemySpell.SpellId);

        result.Success = true;
        result.Method = "SameSpell";
        result.CounterSpellUsed = enemySpell;
        result.LogMessage = $"<color=#00FF00>✨ {counterName} counters {casterName}'s {enemySpell.Name} with their own {enemySpell.Name}! Both spells are negated.</color>";

        CombatUI?.ShowCombatLog(result.LogMessage);
        Debug.Log($"[Counterspell] SAME SPELL COUNTER: {counterName} uses {enemySpell.Name} to counter {casterName}'s {enemySpell.Name}. Automatic success!");

        counterspeller.ClearReadiedCounterspell();
        return result;
    }

    /// <summary>
    /// Resolve a designated counter pair counterspell (e.g., Haste vs Slow).
    /// PHB: Works like same-spell counter — automatic negation.
    /// </summary>
    private CounterspellResult ResolveDesignatedCounterspell(
        CharacterController counterspeller, CharacterController caster,
        SpellData enemySpell, string counterSpellId, CounterspellResult result)
    {
        string counterName = counterspeller.Stats.CharacterName;
        string casterName = caster.Stats.CharacterName;

        SpellData counterSpell = SpellDatabase.GetSpell(counterSpellId);
        string counterSpellName = counterSpell != null ? counterSpell.Name : counterSpellId;

        // Consume counterspeller's spell slot
        bool consumed = counterspeller.ConsumeSpellSlotForCounter(counterSpellId);

        result.Success = true;
        result.Method = "DesignatedCounter";
        result.CounterSpellUsed = counterSpell;
        result.LogMessage = $"<color=#00FF00>✨ {counterName} counters {casterName}'s {enemySpell.Name} with {counterSpellName}! Both spells are negated.</color>";

        CombatUI?.ShowCombatLog(result.LogMessage);
        Debug.Log($"[Counterspell] DESIGNATED COUNTER: {counterName} uses {counterSpellName} to counter {casterName}'s {enemySpell.Name}. Automatic success!");

        counterspeller.ClearReadiedCounterspell();
        return result;
    }

    /// <summary>
    /// Resolve a Dispel Magic counterspell attempt.
    /// PHB: Requires a dispel check (1d20 + CL, max +10) vs DC (11 + enemy CL).
    /// Not automatic — can fail.
    /// </summary>
    private CounterspellResult ResolveDispelMagicCounterspell(
        CharacterController counterspeller, CharacterController caster,
        SpellData enemySpell, CounterspellResult result)
    {
        string counterName = counterspeller.Stats.CharacterName;
        string casterName = caster.Stats.CharacterName;

        int counterCL = Mathf.Max(1, counterspeller.Stats.GetCasterLevel());
        int enemyCL = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Consume Dispel Magic spell slot regardless of success/failure
        bool consumed = counterspeller.ConsumeSpellSlotForCounter(SpellNames.DISPEL_MAGIC);

        // Dispel check: 1d20 + CL (max +10 for Dispel Magic) vs DC 11 + enemy CL
        int clCapped = Mathf.Min(counterCL, 10);
        int d20Roll = DiceService.D20("NPC counterspell dispel check");
        int dispelTotal = d20Roll + clCapped;
        int dispelDC = 11 + enemyCL;

        result.DispelCheckTotal = dispelTotal;
        result.DispelCheckDC = dispelDC;

        SpellData dispelSpell = SpellDatabase.GetSpell(SpellNames.DISPEL_MAGIC);
        result.CounterSpellUsed = dispelSpell;
        result.Method = "DispelMagic";

        bool success = dispelTotal >= dispelDC;
        result.Success = success;

        if (success)
        {
            result.LogMessage = $"<color=#00FF00>✨ {counterName} counters {casterName}'s {enemySpell.Name} with Dispel Magic! " +
                               $"(d20({d20Roll}) + CL {clCapped} = {dispelTotal} vs DC {dispelDC})</color>";
            Debug.Log($"[Counterspell] DISPEL COUNTER SUCCESS: {counterName} dispel check {dispelTotal} >= DC {dispelDC}. {enemySpell.Name} countered!");
        }
        else
        {
            result.LogMessage = $"<color=#FF6666>❌ {counterName} fails to counter {casterName}'s {enemySpell.Name} with Dispel Magic. " +
                               $"(d20({d20Roll}) + CL {clCapped} = {dispelTotal} vs DC {dispelDC})</color>";
            Debug.Log($"[Counterspell] DISPEL COUNTER FAILED: {counterName} dispel check {dispelTotal} < DC {dispelDC}. {enemySpell.Name} resolves normally.");
        }

        CombatUI?.ShowCombatLog(result.LogMessage);
        counterspeller.ClearReadiedCounterspell();
        return result;
    }

    /// <summary>
    /// Expire all readied counterspells for a character (called at start of their turn).
    /// PHB: "Readied action expires at start of your next turn if not used."
    /// </summary>
    public void ExpireReadiedCounterspell(CharacterController character)
    {
        if (character != null && character.HasReadiedCounterspell)
        {
            Debug.Log($"[Counterspell] {character.Stats.CharacterName}: Readied counterspell expired (start of turn).");
            CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏰", $"{character.Stats.CharacterName}'s readied counterspell expires."));
            character.ClearReadiedCounterspell();
        }
    }
}
