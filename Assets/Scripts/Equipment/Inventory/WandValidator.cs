using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates whether a character can use a wand per D&D 3.5e DMG rules.
///
/// Wand activation is MUCH simpler than scrolls:
/// 1. Spell trigger activation: spell must be on the character's class spell list
///    (even if the character is too low-level to actually cast that spell)
/// 2. No ability score check required (unlike scrolls)
/// 3. No caster level check required (unlike scrolls — wands always work at wand's CL)
/// 4. No arcane/divine type matching required (any class with the spell on its list qualifies)
/// 5. Use Magic Device (DC 20, flat) can bypass the class list requirement
/// 6. Wand activation is a standard action that does NOT provoke AoO (unlike scrolls/potions)
///
/// Magic Domain (D&D 3.5e PHB):
/// A cleric with the Magic domain can use spell trigger items (wands) as if they were a wizard
/// of half their cleric level. This allows them to use arcane wands even if the spell isn't on
/// the cleric list. No ability score check is needed for wands (regardless of Magic domain).
/// </summary>
public static class WandValidator
{
    /// <summary>
    /// Result of wand usage validation.
    /// </summary>
    public class WandValidationResult
    {
        public bool CanUse;
        public string FailureReason;
        public string MatchedClassName;     // Which class grants access (or "Use Magic Device")
        public bool NeedsUMDCheck;          // True if UMD is the only path to use this wand
        public bool UsedUMD;                // True if UMD was used to bypass requirements
        public bool UsedMagicDomain;        // True if Magic domain power was used

        /// <summary>Format a short summary for combat log.</summary>
        public string GetSummary()
        {
            if (!CanUse)
                return FailureReason ?? "Cannot use this wand.";
            if (UsedUMD)
                return "Using wand via Use Magic Device.";
            if (UsedMagicDomain)
                return $"Uses Magic domain power to activate arcane wand as a wizard of level {EffectiveWizardLevel}.";
            return $"Wand activated as {MatchedClassName}.";
        }

        /// <summary>Effective wizard level from Magic domain (0 if not used).</summary>
        public int EffectiveWizardLevel;
    }

    // All caster classes — wands don't care about arcane vs divine distinction
    private static readonly string[] AllCasterClasses = { "Wizard", "Sorcerer", "Bard", "Cleric", "Druid", "Paladin", "Ranger" };

    // ── Public API ──

    /// <summary>
    /// Validate whether a character can use the given wand item.
    /// Spell trigger items (wands) require the spell to be on the character's class list,
    /// OR a successful Use Magic Device check (DC 20).
    /// No ability score check. No caster level check. No type matching.
    /// </summary>
    public static WandValidationResult Validate(CharacterController character, ItemData wandItem)
    {
        var result = new WandValidationResult { CanUse = false };

        if (character == null || character.Stats == null)
        {
            result.FailureReason = "No active character.";
            return result;
        }

        if (wandItem == null || !wandItem.IsWand)
        {
            result.FailureReason = "This is not a wand.";
            return result;
        }

        // Check if wand is depleted
        if (wandItem.CurrentCharges <= 0)
        {
            result.FailureReason = $"{wandItem.Name} is depleted (0 charges remaining). It is now a useless nonmagical stick.";
            return result;
        }

        CharacterStats stats = character.Stats;

        // Look up the spell to check class lists
        SpellDatabase.Init();
        string spellName = wandItem.ConsumableSpellName;
        if (string.IsNullOrWhiteSpace(spellName) && !string.IsNullOrWhiteSpace(wandItem.WandSpellId))
        {
            SpellData spellById = SpellDatabase.GetSpell(wandItem.WandSpellId);
            if (spellById != null) spellName = spellById.Name;
        }

        SpellData spell = null;
        if (!string.IsNullOrWhiteSpace(spellName))
            spell = SpellDatabase.GetSpellByName(spellName);

        if (spell == null)
        {
            result.FailureReason = $"Spell data not found for wand '{wandItem.Name}'.";
            return result;
        }

        // Step 1: Check if spell is on ANY of the character's class spell lists
        // Spell trigger: character just needs the spell on their class list —
        // they don't need to be high enough level to actually cast it
        foreach (string cls in AllCasterClasses)
        {
            int classLevel = stats.GetClassLevel(cls);
            if (classLevel <= 0) continue;

            int spellLevelForClass = spell.GetSpellLevelFor(cls);
            if (spellLevelForClass < 0) continue;

            // Character has this class AND the spell is on that class's list — success!
            result.CanUse = true;
            result.MatchedClassName = cls;
            return result;
        }

        // ── Magic Domain check (D&D 3.5e PHB) ──
        // A cleric with the Magic domain can use spell trigger items (wands) as if
        // they were a wizard of half their cleric level. Check if the spell is on the
        // wizard list — if so, grant access via Magic domain power.
        if (stats.HasMagicDomain)
        {
            int effectiveWizLevel = stats.MagicDomainEffectiveWizardLevel;
            if (effectiveWizLevel > 0)
            {
                int wizSpellLevel = spell.GetSpellLevelFor("Wizard");
                if (wizSpellLevel >= 0)
                {
                    result.CanUse = true;
                    result.MatchedClassName = "Wizard (Magic Domain)";
                    result.UsedMagicDomain = true;
                    result.EffectiveWizardLevel = effectiveWizLevel;
                    return result;
                }
            }
        }

        // Step 2: No class match and no Magic domain match — check if Use Magic Device is available
        int umdBonus = stats.GetSkillBonus("Use Magic Device");
        if (umdBonus > 0)
        {
            // UMD is available — flag that a UMD check (DC 20) will be needed
            result.CanUse = true;
            result.NeedsUMDCheck = true;
            result.MatchedClassName = "Use Magic Device";
            return result;
        }

        // No class access, no Magic domain, and no UMD skill
        result.FailureReason = $"{stats.CharacterName} cannot use {wandItem.Name}: " +
                               $"the spell is not on any of their class spell lists. " +
                               $"(Requires any caster class with this spell on its list, or Use Magic Device skill)";
        return result;
    }

    /// <summary>
    /// Perform a Use Magic Device check to activate a wand.
    /// DC = 20 (flat, per DMG — wand UMD does NOT scale with caster level unlike scrolls).
    /// </summary>
    public static bool PerformUMDCheck(CharacterController character, ItemData wandItem, out string summary)
    {
        summary = "";
        if (character == null || character.Stats == null || wandItem == null)
        {
            summary = "Invalid character or wand.";
            return false;
        }

        int umdBonus = character.Stats.GetSkillBonus("Use Magic Device");
        int dc = 20; // Flat DC 20 for wand activation via UMD
        int d20 = DiceRoller.D20();
        int total = d20 + umdBonus;

        if (total >= dc)
        {
            summary = $"Use Magic Device check passed (d20={d20}+{umdBonus}={total} vs DC {dc}).";
            return true;
        }
        else
        {
            summary = $"Use Magic Device check failed (d20={d20}+{umdBonus}={total} vs DC {dc}). " +
                       "Cannot activate this wand via UMD this round.";
            return false;
        }
    }
}
