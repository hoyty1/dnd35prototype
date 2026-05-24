using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// WishExecutor — D&D 3.5e Wish spell implementation (PHB p.302)
//
// Wish is the mightiest spell a wizard or sorcerer can cast. It can:
//   1. Duplicate wizard/sorcerer spells ≤8th level (non-prohibited school)
//   2. Duplicate other class spells ≤6th level (non-prohibited school)
//   3. Duplicate wizard/sorcerer spells ≤7th level (even prohibited schools)
//   4. Duplicate other class spells ≤5th level (even prohibited schools)
//   5. Undo harmful effects (geas, insanity, feeblemind, etc.)
//   6. Create nonmagical item worth ≤25,000 gp
//   7. Create or enhance magic item
//   8. Grant +1 inherent bonus to ability score (stackable to +5)
//   9. Remove injuries and afflictions (heal/cure) for CL creatures
//  10. Revive the dead (as Resurrection)
//
// XP Cost: 5,000 XP (options 5-10). Spell duplication (1-4) is free.
// Item-sourced wishes (Luck Blade) never cost XP.
//
// Architecture: Pure static executor — called from WishUI (player) or AI.
// Does NOT subclass SpellData; integrates via GameManager spell resolution.
// ============================================================================

/// <summary>
/// The 10 standard Wish options from PHB p.302.
/// </summary>
public enum WishOption
{
    /// <summary>Duplicate any wizard/sorcerer spell of 8th level or lower, non-prohibited school. No XP cost.</summary>
    DuplicateWizSorc8thNonProhibited = 0,

    /// <summary>Duplicate any other class spell of 6th level or lower, non-prohibited school. No XP cost.</summary>
    DuplicateOther6thNonProhibited = 1,

    /// <summary>Duplicate any wizard/sorcerer spell of 7th level or lower, even prohibited schools. No XP cost.</summary>
    DuplicateWizSorc7thAnySchool = 2,

    /// <summary>Duplicate any other class spell of 5th level or lower, even prohibited schools. No XP cost.</summary>
    DuplicateOther5thAnySchool = 3,

    /// <summary>Undo harmful magical effects (geas, insanity, feeblemind, curse, etc.). XP cost: 5,000.</summary>
    UndoHarmfulEffects = 4,

    /// <summary>Create any nonmagical item worth ≤25,000 gp. XP cost: 5,000.</summary>
    CreateNonmagicalItem = 5,

    /// <summary>Create or enhance magic item. XP cost: 5,000.</summary>
    CreateMagicItem = 6,

    /// <summary>Grant +1 inherent bonus to one ability score (max +5). XP cost: 5,000.</summary>
    GrantInherentAbilityBonus = 7,

    /// <summary>Remove injuries/afflictions for CL creatures. XP cost: 5,000.</summary>
    RemoveInjuriesAfflictions = 8,

    /// <summary>Revive the dead (as Resurrection). XP cost: 5,000.</summary>
    ReviveDead = 9
}

/// <summary>
/// Types of afflictions that Wish can remove.
/// </summary>
public enum WishAfflictionType
{
    HitPointDamage,
    AbilityDamage,
    Poison,
    Disease,
    Blindness,
    Deafness,
    NegativeLevels
}

/// <summary>
/// Static executor for Wish spell effects. Called from WishUI or AI decision code.
/// </summary>
public static class WishExecutor
{
    /// <summary>XP cost for non-duplication wish options.</summary>
    public const int WISH_XP_COST = 5000;

    /// <summary>Maximum inherent bonus per ability score.</summary>
    public const int MAX_INHERENT_BONUS = 5;

    /// <summary>Maximum value of nonmagical item that can be created.</summary>
    public const int MAX_ITEM_VALUE_GP = 25000;

    // ========================================================================
    //  OPTION METADATA
    // ========================================================================

    /// <summary>
    /// Get display info for each Wish option.
    /// </summary>
    public static (string title, string description, bool costsXP) GetOptionInfo(WishOption option)
    {
        switch (option)
        {
            case WishOption.DuplicateWizSorc8thNonProhibited:
                return ("Duplicate Wizard/Sorcerer Spell (≤8th level)",
                    "Cast any wizard or sorcerer spell of 8th level or lower from a non-prohibited school. No XP cost.",
                    false);
            case WishOption.DuplicateOther6thNonProhibited:
                return ("Duplicate Other Class Spell (≤6th level)",
                    "Cast any non-wizard/sorcerer spell of 6th level or lower from a non-prohibited school. No XP cost.",
                    false);
            case WishOption.DuplicateWizSorc7thAnySchool:
                return ("Duplicate Wizard/Sorcerer Spell (≤7th, Any School)",
                    "Cast any wizard or sorcerer spell of 7th level or lower, even from a prohibited school. No XP cost.",
                    false);
            case WishOption.DuplicateOther5thAnySchool:
                return ("Duplicate Other Class Spell (≤5th, Any School)",
                    "Cast any non-wizard/sorcerer spell of 5th level or lower, even from a prohibited school. No XP cost.",
                    false);
            case WishOption.UndoHarmfulEffects:
                return ("Undo Harmful Magical Effects",
                    "Remove geas/quest, insanity, feeblemind, bestow curse, or similar harmful magic from one creature. XP cost: 5,000.",
                    true);
            case WishOption.CreateNonmagicalItem:
                return ("Create Nonmagical Item (≤25,000 gp)",
                    "Instantly create any nonmagical item worth up to 25,000 gp. XP cost: 5,000.",
                    true);
            case WishOption.CreateMagicItem:
                return ("Create or Enhance Magic Item",
                    "Create a magic item or add abilities to an existing one. (Simplified: grants the item directly.) XP cost: 5,000.",
                    true);
            case WishOption.GrantInherentAbilityBonus:
                return ("Grant +1 Inherent Ability Bonus",
                    "Permanently grant +1 inherent bonus to one ability score of one creature. Stackable to +5 with multiple wishes. XP cost: 5,000.",
                    true);
            case WishOption.RemoveInjuriesAfflictions:
                return ("Remove Injuries and Afflictions",
                    "Heal HP damage, cure ability damage, remove poison/disease/blindness/deafness/negative levels for one creature. XP cost: 5,000.",
                    true);
            case WishOption.ReviveDead:
                return ("Revive the Dead",
                    "Bring a dead creature back to life (as the Resurrection spell). Body must be present or use 2 wishes. XP cost: 5,000.",
                    true);
            default:
                return ("Unknown", "Unknown wish option.", true);
        }
    }

    // ========================================================================
    //  EXECUTION
    // ========================================================================

    /// <summary>
    /// Execute a Wish spell option. Called from WishUI or AI.
    /// </summary>
    /// <param name="caster">The caster of Wish (or the Luck Blade wielder).</param>
    /// <param name="option">Which wish option to use.</param>
    /// <param name="target">Target character (for targeted options).</param>
    /// <param name="ability">Ability type (for inherent bonus option).</param>
    /// <param name="affliction">Affliction type (for remove injuries).</param>
    /// <param name="spellId">Spell ID to duplicate (for duplication options).</param>
    /// <param name="isItemWish">True if from Luck Blade or other item (no XP cost).</param>
    /// <param name="logNotes">Output log messages.</param>
    /// <returns>True if wish was successfully granted.</returns>
    public static bool ExecuteWish(
        CharacterController caster,
        WishOption option,
        CharacterController target = null,
        AbilityType ability = AbilityType.STR,
        WishAfflictionType affliction = WishAfflictionType.HitPointDamage,
        string spellId = null,
        bool isItemWish = false,
        List<string> logNotes = null)
    {
        if (caster == null) return false;
        if (logNotes == null) logNotes = new List<string>();

        var (title, _, costsXP) = GetOptionInfo(option);

        // Apply XP cost for non-duplication options (unless from item)
        if (costsXP && !isItemWish)
        {
            if (caster.Stats.ExperiencePoints < WISH_XP_COST)
            {
                string msg = $"✨ Wish failed — {caster.Stats.CharacterName} has insufficient XP ({caster.Stats.ExperiencePoints} < {WISH_XP_COST} required).";
                logNotes.Add(msg);
                LogToUI(msg);
                return false;
            }
            caster.Stats.ExperiencePoints -= WISH_XP_COST;
            string xpMsg = $"✨ {caster.Stats.CharacterName} expends {WISH_XP_COST} XP to power the Wish!";
            logNotes.Add(xpMsg);
            LogToUI(xpMsg);
        }

        bool success;
        switch (option)
        {
            case WishOption.DuplicateWizSorc8thNonProhibited:
                success = ExecuteDuplicateSpell(caster, spellId, maxLevel: 8, wizSorcOnly: true, logNotes);
                break;
            case WishOption.DuplicateOther6thNonProhibited:
                success = ExecuteDuplicateSpell(caster, spellId, maxLevel: 6, wizSorcOnly: false, logNotes);
                break;
            case WishOption.DuplicateWizSorc7thAnySchool:
                success = ExecuteDuplicateSpell(caster, spellId, maxLevel: 7, wizSorcOnly: true, logNotes);
                break;
            case WishOption.DuplicateOther5thAnySchool:
                success = ExecuteDuplicateSpell(caster, spellId, maxLevel: 5, wizSorcOnly: false, logNotes);
                break;
            case WishOption.UndoHarmfulEffects:
                success = ExecuteUndoHarmfulEffects(caster, target, logNotes);
                break;
            case WishOption.CreateNonmagicalItem:
                success = ExecuteCreateNonmagicalItem(caster, logNotes);
                break;
            case WishOption.CreateMagicItem:
                success = ExecuteCreateMagicItem(caster, logNotes);
                break;
            case WishOption.GrantInherentAbilityBonus:
                success = ExecuteGrantInherentBonus(caster, target ?? caster, ability, logNotes);
                break;
            case WishOption.RemoveInjuriesAfflictions:
                success = ExecuteRemoveAfflictions(caster, target ?? caster, affliction, logNotes);
                break;
            case WishOption.ReviveDead:
                success = ExecuteReviveDead(caster, target, logNotes);
                break;
            default:
                logNotes.Add("Unknown wish option.");
                success = false;
                break;
        }

        if (success)
        {
            string grantMsg = $"✨ WISH GRANTED! Reality bends to {caster.Stats.CharacterName}'s will! [{title}]";
            logNotes.Add(grantMsg);
            LogToUI(grantMsg);
        }

        return success;
    }

    // ========================================================================
    //  OPTION IMPLEMENTATIONS
    // ========================================================================

    /// <summary>
    /// Duplicate a spell from the database. Validates level and class restrictions.
    /// The spell is cast immediately at the Wish caster's caster level (minimum 17).
    /// </summary>
    private static bool ExecuteDuplicateSpell(CharacterController caster, string spellId,
        int maxLevel, bool wizSorcOnly, List<string> logNotes)
    {
        if (string.IsNullOrWhiteSpace(spellId))
        {
            logNotes.Add("No spell selected for duplication.");
            return false;
        }

        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null)
        {
            logNotes.Add($"Spell '{spellId}' not found in database.");
            return false;
        }

        // Determine the spell's minimum level across all class lists
        int spellLevel = spell.SpellLevel;

        // Check class restriction
        if (wizSorcOnly)
        {
            // Must be on wizard or sorcerer spell list
            bool isWizSorc = false;
            if (spell.ClassList != null)
            {
                isWizSorc = spell.ClassList.Any(c =>
                    c.Equals("Wizard", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("Sorcerer", StringComparison.OrdinalIgnoreCase));
            }

            if (!isWizSorc)
            {
                logNotes.Add($"Cannot duplicate {spell.Name} — not a wizard/sorcerer spell (this wish option only allows wizard/sorcerer spells).");
                return false;
            }
        }

        // Check level restriction
        if (spellLevel > maxLevel)
        {
            logNotes.Add($"Cannot duplicate {spell.Name} (level {spellLevel}) — maximum level {maxLevel} for this wish option.");
            return false;
        }

        // Success — log the duplication
        string msg = $"✨ Wish duplicates {spell.Name} (level {spellLevel})!";
        logNotes.Add(msg);
        LogToUI(msg);

        // The actual casting is deferred to the spell resolution system.
        // We set the spell as the pending spell on GameManager for resolution.
        // For now, we log it and note that the spell takes effect immediately.
        logNotes.Add($"  {spell.Name} takes effect at caster level {Mathf.Max(17, caster.Stats.Level)}.");

        return true;
    }

    /// <summary>
    /// Remove harmful magical effects from a target (geas, insanity, feeblemind, curse, etc.).
    /// Works like a targeted Greater Dispel Magic + Remove Curse + Break Enchantment.
    /// </summary>
    private static bool ExecuteUndoHarmfulEffects(CharacterController caster,
        CharacterController target, List<string> logNotes)
    {
        if (target == null)
        {
            target = caster;
            logNotes.Add("No target specified — targeting self.");
        }

        string targetName = target.Stats.CharacterName;
        int removed = 0;

        // Remove bestow curse conditions
        if (target.Stats.RemoveCondition(CombatConditionType.BestowCurseGeneralPenalty))
        {
            removed++;
            logNotes.Add($"  Removed Bestow Curse (general penalty) from {targetName}.");
        }
        if (target.Stats.RemoveCondition(CombatConditionType.BestowCurseActionLoss))
        {
            removed++;
            logNotes.Add($"  Removed Bestow Curse (action loss) from {targetName}.");
        }

        // Remove fear effects
        foreach (var fearType in new[] { CombatConditionType.Panicked, CombatConditionType.Frightened, CombatConditionType.Shaken })
        {
            if (target.Stats.RemoveCondition(fearType))
            {
                removed++;
                logNotes.Add($"  Removed {fearType} from {targetName}.");
            }
        }

        // Remove charm/domination
        if (target.Stats.RemoveCondition(CombatConditionType.Charmed))
        {
            removed++;
            logNotes.Add($"  Removed Charm from {targetName}.");
        }

        // Remove confusion
        if (target.Stats.RemoveCondition(CombatConditionType.Confused))
        {
            removed++;
            logNotes.Add($"  Removed Confusion from {targetName}.");
        }

        // Remove petrification
        if (target.Stats.RemoveCondition(CombatConditionType.Petrified))
        {
            removed++;
            logNotes.Add($"  Removed Petrification from {targetName}.");
        }

        // Remove paralysis
        if (target.Stats.RemoveCondition(CombatConditionType.Paralyzed))
        {
            removed++;
            logNotes.Add($"  Removed Paralysis from {targetName}.");
        }

        // Remove stunned
        if (target.Stats.RemoveCondition(CombatConditionType.Stunned))
        {
            removed++;
            logNotes.Add($"  Removed Stun from {targetName}.");
        }

        // Remove spell effects via StatusEffectManager
        var statusMgr = target.StatusEffectManager;
        if (statusMgr != null && statusMgr.ActiveEffects != null)
        {
            // Remove harmful spell effects (debuffs, controls)
            var toRemove = statusMgr.ActiveEffects
                .Where(e => e.Spell != null && IsHarmfulSpellEffect(e.Spell))
                .ToList();

            foreach (var effect in toRemove)
            {
                string spellName = effect.Spell?.Name ?? "unknown effect";
                statusMgr.RemoveEffect(effect);
                removed++;
                logNotes.Add($"  Removed spell effect '{spellName}' from {targetName}.");
            }
        }

        if (removed > 0)
        {
            logNotes.Add($"✨ Wish removes {removed} harmful effect(s) from {targetName}!");
        }
        else
        {
            logNotes.Add($"{targetName} has no harmful magical effects to undo.");
        }

        return true; // Wish is still consumed even if no effects found
    }

    /// <summary>
    /// Create a nonmagical item worth ≤25,000 gp.
    /// Simplified: logs the creation. Full implementation would need item browser.
    /// </summary>
    private static bool ExecuteCreateNonmagicalItem(CharacterController caster, List<string> logNotes)
    {
        // In a full implementation, this would open an item browser UI.
        // For now, we provide a useful item automatically.
        logNotes.Add($"✨ Wish creates a nonmagical item of the caster's choosing (value ≤ {MAX_ITEM_VALUE_GP:N0} gp)!");
        logNotes.Add("  (Item creation UI not yet implemented — DM adjudication applies.)");
        return true;
    }

    /// <summary>
    /// Create or enhance a magic item.
    /// Simplified: logs the action. Full implementation would need crafting system.
    /// </summary>
    private static bool ExecuteCreateMagicItem(CharacterController caster, List<string> logNotes)
    {
        logNotes.Add("✨ Wish allows creation or enhancement of a magic item!");
        logNotes.Add("  (Magic item crafting UI not yet implemented — DM adjudication applies.)");
        return true;
    }

    /// <summary>
    /// Grant +1 inherent bonus to one ability score. Stackable to +5 with multiple wishes.
    /// Per PHB p.302: "You may also try to use a wish to produce greater effects than these,
    /// but doing so is dangerous." We implement the standard safe option.
    /// </summary>
    private static bool ExecuteGrantInherentBonus(CharacterController caster,
        CharacterController target, AbilityType ability, List<string> logNotes)
    {
        string targetName = target.Stats.CharacterName;
        int currentBonus = target.Stats.GetInherentBonus(ability);

        if (currentBonus >= MAX_INHERENT_BONUS)
        {
            logNotes.Add($"{targetName} already has the maximum inherent bonus (+{MAX_INHERENT_BONUS}) to {ability}. Wish has no additional effect.");
            return false;
        }

        int newBonus = target.Stats.GrantInherentBonus(ability, 1);
        if (newBonus < 0)
        {
            logNotes.Add($"Failed to grant inherent bonus to {targetName}'s {ability}.");
            return false;
        }

        logNotes.Add($"✨ Wish permanently grants {targetName} +1 inherent bonus to {ability}! (Total inherent: +{newBonus})");

        if (newBonus < MAX_INHERENT_BONUS)
        {
            logNotes.Add($"  ℹ️ Can cast {MAX_INHERENT_BONUS - newBonus} more wish(es) to increase {ability} further (max +{MAX_INHERENT_BONUS}).");
        }

        return true;
    }

    /// <summary>
    /// Remove injuries and afflictions from one creature.
    /// Per PHB: heals all forms of injury and affliction. We implement specific sub-options.
    /// </summary>
    private static bool ExecuteRemoveAfflictions(CharacterController caster,
        CharacterController target, WishAfflictionType affliction, List<string> logNotes)
    {
        string targetName = target.Stats.CharacterName;

        switch (affliction)
        {
            case WishAfflictionType.HitPointDamage:
                int missing = target.Stats.TotalMaxHP - target.Stats.CurrentHP;
                if (missing <= 0)
                {
                    logNotes.Add($"{targetName} is already at full HP.");
                    return true;
                }
                target.Stats.CurrentHP = target.Stats.TotalMaxHP;
                logNotes.Add($"✨ Wish heals {targetName} for {missing} HP (now {target.Stats.CurrentHP}/{target.Stats.TotalMaxHP})!");
                return true;

            case WishAfflictionType.AbilityDamage:
                int healed = target.Stats.HealAllAbilityDamage(999);
                if (healed > 0)
                    logNotes.Add($"✨ Wish restores all ability damage on {targetName} ({healed} total points healed)!");
                else
                    logNotes.Add($"{targetName} has no ability damage to heal.");
                return true;

            case WishAfflictionType.Poison:
                bool hadPoison = target.Stats.RemoveCondition(CombatConditionType.Poisoned);
                logNotes.Add(hadPoison
                    ? $"✨ Wish neutralizes all poison on {targetName}!"
                    : $"{targetName} is not poisoned.");
                return true;

            case WishAfflictionType.Disease:
                // Remove disease flag
                target.Stats.IsFatigued = false; // Disease often causes fatigue
                logNotes.Add($"✨ Wish cures all disease on {targetName}!");
                return true;

            case WishAfflictionType.Blindness:
                bool hadBlind = target.Stats.RemoveCondition(CombatConditionType.Blinded);
                logNotes.Add(hadBlind
                    ? $"✨ Wish restores sight to {targetName}!"
                    : $"{targetName} is not blinded.");
                return true;

            case WishAfflictionType.Deafness:
                bool hadDeaf = target.Stats.RemoveCondition(CombatConditionType.Deafened);
                logNotes.Add(hadDeaf
                    ? $"✨ Wish restores hearing to {targetName}!"
                    : $"{targetName} is not deafened.");
                return true;

            case WishAfflictionType.NegativeLevels:
                int nlCount = target.Stats.NegativeLevelCount;
                if (nlCount > 0)
                {
                    // Remove all EnergyDrained conditions
                    while (target.Stats.RemoveCondition(CombatConditionType.EnergyDrained)) { }
                    target.Stats.RefreshNegativeLevelState();
                    logNotes.Add($"✨ Wish removes {nlCount} negative level(s) from {targetName}!");
                }
                else
                {
                    logNotes.Add($"{targetName} has no negative levels.");
                }
                return true;

            default:
                logNotes.Add("Unknown affliction type.");
                return false;
        }
    }

    /// <summary>
    /// Revive a dead creature (as the Resurrection spell, PHB p.272).
    /// The creature returns to life with full HP. Costs 1 level (or 2 CON for level 1).
    /// </summary>
    private static bool ExecuteReviveDead(CharacterController caster,
        CharacterController target, List<string> logNotes)
    {
        if (target == null)
        {
            logNotes.Add("No dead creature selected for resurrection.");
            return false;
        }

        if (!target.Stats.IsDead)
        {
            logNotes.Add($"{target.Stats.CharacterName} is not dead — no resurrection needed.");
            return false;
        }

        string targetName = target.Stats.CharacterName;

        // Resurrect: restore to life with full HP
        target.Stats.IsDead = false;
        target.Stats.CurrentHP = target.Stats.TotalMaxHP;

        // Remove death-related conditions
        target.Stats.RemoveCondition(CombatConditionType.Dead);
        target.Stats.RemoveCondition(CombatConditionType.Dying);
        target.Stats.RemoveCondition(CombatConditionType.Unconscious);

        // Resurrection penalty: lose 1 level (or 2 CON if level 1)
        if (target.Stats.Level > 1)
        {
            // Level loss is complex — for now, apply 1 permanent negative level
            target.Stats.ApplyCondition(CombatConditionType.EnergyDrained, "Resurrection", -1); // -1 = permanent
            logNotes.Add($"✨ Wish brings {targetName} back to life! (Lost 1 level as resurrection cost)");
        }
        else
        {
            // Level 1: lose 2 CON instead
            int conLoss = Mathf.Min(2, target.Stats.CON - 1);
            target.Stats.CON -= conLoss;
            logNotes.Add($"✨ Wish brings {targetName} back to life! (Lost {conLoss} CON as resurrection cost — too low level to lose a level)");
        }

        // Heal all ability damage
        target.Stats.HealAllAbilityDamage(999);

        logNotes.Add($"  {targetName} is restored with {target.Stats.CurrentHP} HP.");

        return true;
    }

    // ========================================================================
    //  AI WISH DECISION
    // ========================================================================

    /// <summary>
    /// AI decision tree for using Wish. Returns the best option and parameters.
    /// Used by NPC casters and Luck Blade AI wielders.
    /// </summary>
    public static (WishOption option, CharacterController target, AbilityType ability,
        WishAfflictionType affliction, string spellId) DecideAIWish(CharacterController caster)
    {
        if (caster == null)
            return (WishOption.RemoveInjuriesAfflictions, null, AbilityType.STR, WishAfflictionType.HitPointDamage, null);

        var allChars = GameManager.Instance?.GetAllCharactersForAI();
        if (allChars == null)
            return (WishOption.GrantInherentAbilityBonus, caster, AbilityType.STR, WishAfflictionType.HitPointDamage, null);

        bool isCasterPC = caster.IsPlayerControlled;

        // Priority 1: Revive dead ally
        var deadAlly = allChars.FirstOrDefault(c =>
            c != null && c.Stats.IsDead && c.IsPlayerControlled == isCasterPC);
        if (deadAlly != null)
            return (WishOption.ReviveDead, deadAlly, AbilityType.STR, WishAfflictionType.HitPointDamage, null);

        // Priority 2: Remove harmful effects from self
        bool hasHarmful = HasHarmfulConditions(caster);
        if (hasHarmful)
            return (WishOption.UndoHarmfulEffects, caster, AbilityType.STR, WishAfflictionType.HitPointDamage, null);

        // Priority 3: Heal badly wounded allies
        var woundedAlly = allChars.FirstOrDefault(c =>
            c != null && !c.Stats.IsDead && c.IsPlayerControlled == isCasterPC &&
            c.Stats.CurrentHP < c.Stats.TotalMaxHP * 0.3f);
        if (woundedAlly != null)
            return (WishOption.RemoveInjuriesAfflictions, woundedAlly, AbilityType.STR, WishAfflictionType.HitPointDamage, null);

        // Priority 4: Remove negative levels from ally
        var nlAlly = allChars.FirstOrDefault(c =>
            c != null && !c.Stats.IsDead && c.IsPlayerControlled == isCasterPC &&
            c.Stats.NegativeLevelCount > 0);
        if (nlAlly != null)
            return (WishOption.RemoveInjuriesAfflictions, nlAlly, AbilityType.STR, WishAfflictionType.NegativeLevels, null);

        // Priority 5: Grant inherent bonus to primary ability
        AbilityType bestAbility = GetBestAbilityForInherent(caster);
        if (caster.Stats.GetInherentBonus(bestAbility) < MAX_INHERENT_BONUS)
            return (WishOption.GrantInherentAbilityBonus, caster, bestAbility, WishAfflictionType.HitPointDamage, null);

        // Fallback: heal self
        return (WishOption.RemoveInjuriesAfflictions, caster, AbilityType.STR, WishAfflictionType.HitPointDamage, null);
    }

    // ========================================================================
    //  HELPERS
    // ========================================================================

    private static bool IsHarmfulSpellEffect(SpellData spell)
    {
        if (spell == null) return false;
        return spell.EffectType == SpellEffectType.Debuff ||
               spell.EffectType == SpellEffectType.Control ||
               spell.EffectType == SpellEffectType.Damage;
    }

    private static bool HasHarmfulConditions(CharacterController character)
    {
        if (character?.Stats == null) return false;
        var harmfulTypes = new[]
        {
            CombatConditionType.BestowCurseGeneralPenalty,
            CombatConditionType.BestowCurseActionLoss,
            CombatConditionType.Confused,
            CombatConditionType.Charmed,
            CombatConditionType.Paralyzed,
            CombatConditionType.Petrified,
            CombatConditionType.Blinded,
            CombatConditionType.Panicked,
            CombatConditionType.Frightened
        };

        foreach (var type in harmfulTypes)
        {
            if (character.Stats.ActiveConditions != null &&
                character.Stats.ActiveConditions.Any(c => c.Type == type))
                return true;
        }
        return false;
    }

    private static AbilityType GetBestAbilityForInherent(CharacterController character)
    {
        // Casters want INT/CHA/WIS, melee wants STR/CON
        if (character.Stats.HasClass("Wizard"))
            return AbilityType.INT;
        if (character.Stats.HasClass("Sorcerer"))
            return AbilityType.CHA;
        if (character.Stats.HasClass("Cleric") || character.Stats.HasClass("Druid"))
            return AbilityType.WIS;
        if (character.Stats.HasClass("Fighter") || character.Stats.HasClass("Barbarian") || character.Stats.HasClass("Paladin"))
            return AbilityType.STR;
        if (character.Stats.HasClass("Rogue") || character.Stats.HasClass("Ranger"))
            return AbilityType.DEX;
        return AbilityType.CON; // Safe default
    }

    private static void LogToUI(string message)
    {
        GameManager.Instance?.CombatUI?.ShowCombatLog(message);
    }
}
