using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates whether a character can use a spell scroll per D&D 3.5e DMG rules.
/// 
/// Requirements to use a scroll without UMD:
/// 1. Spell must be on the character's class spell list
/// 2. Scroll must match caster type (arcane class → arcane scroll, divine class → divine scroll)
/// 3. Character must have requisite ability score (10 + spell level) in their casting stat
/// 4. If character's CL < scroll's CL, must pass a caster level check: d20 + CL ≥ scroll CL + 1
///    - Natural 1 on d20 is automatic failure
///    - On failure: DC 5 Wisdom check or scroll mishap
///
/// Use Magic Device (DC 20 + scroll CL) can bypass all requirements.
///
/// Magic Domain (D&D 3.5e PHB):
/// A cleric with the Magic domain can use spell completion (scrolls) and spell trigger (wands)
/// items as if they were a wizard of half their cleric level. This allows them to use arcane
/// scrolls even if the spell isn't on the cleric list. They still need the requisite INT score
/// for arcane scrolls (10 + spell level) and may need a caster level check if their effective
/// wizard level is lower than the scroll's CL.
/// </summary>
public static class ScrollValidator
{
    /// <summary>
    /// Result of scroll usage validation.
    /// </summary>
    public class ScrollValidationResult
    {
        public bool CanUse;
        public bool NeedsCasterLevelCheck;
        public int CasterLevelCheckDC;
        public string FailureReason;
        public string MatchedClassName;     // Which class grants access
        public int CharacterCasterLevel;    // Character's CL for the matched class
        public bool UsedUMD;                // True if UMD was used to bypass requirements
        public bool UsedMagicDomain;        // True if Magic domain power was used

        /// <summary>Format a short summary for combat log.</summary>
        public string GetSummary()
        {
            if (!CanUse)
                return FailureReason ?? "Cannot use this scroll.";
            if (UsedUMD)
                return $"Using scroll via Use Magic Device.";
            if (UsedMagicDomain)
            {
                string clCheck = NeedsCasterLevelCheck
                    ? $" Caster level check needed (d20+{CharacterCasterLevel} vs DC {CasterLevelCheckDC})."
                    : "";
                return $"Uses Magic domain power to activate arcane scroll as a wizard of level {CharacterCasterLevel}.{clCheck}";
            }
            if (NeedsCasterLevelCheck)
                return $"Caster level check needed (d20+{CharacterCasterLevel} vs DC {CasterLevelCheckDC}).";
            return $"Scroll activated as {MatchedClassName} (CL {CharacterCasterLevel}).";
        }
    }

    /// <summary>
    /// Result of a caster level check when using a scroll above character's CL.
    /// </summary>
    public class CasterLevelCheckResult
    {
        public bool Success;
        public int D20Roll;
        public int TotalCheck;
        public int DC;
        public bool Mishap;           // True if mishap occurred (failed CL check + failed Wis check)
        public int MishapDamage;      // Damage from mishap (1d6 per spell level)
        public string Summary;
    }

    // ── Arcane / Divine class mappings ──
    private static readonly string[] ArcaneClasses = { "Wizard", "Sorcerer", "Bard" };
    private static readonly string[] DivineClasses = { "Cleric", "Druid", "Paladin", "Ranger" };

    // ── Casting ability by class ──
    private static readonly Dictionary<string, string> CastingAbility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Wizard", "INT" },
        { "Sorcerer", "CHA" },
        { "Bard", "CHA" },
        { "Cleric", "WIS" },
        { "Druid", "WIS" },
        { "Paladin", "WIS" },
        { "Ranger", "WIS" }
    };

    // ── Public API ──

    /// <summary>
    /// Validate whether a character can use the given scroll item.
    /// </summary>
    public static ScrollValidationResult Validate(CharacterController character, ItemData scrollItem)
    {
        var result = new ScrollValidationResult { CanUse = false };

        if (character == null || character.Stats == null)
        {
            result.FailureReason = "No active character.";
            return result;
        }

        if (scrollItem == null || !scrollItem.IsScroll)
        {
            result.FailureReason = "This is not a scroll.";
            return result;
        }

        CharacterStats stats = character.Stats;
        SpellDatabase.Init();

        // Use unified ScrollData when available, fall back to legacy fields
        SpellData spell = scrollItem.Scroll?.GetSpell()
                          ?? SpellDatabase.GetSpell(scrollItem.ConsumableSpellName)
                          ?? SpellDatabase.GetSpellByName(scrollItem.ConsumableSpellName);
        if (spell == null)
        {
            string spellRef = scrollItem.Scroll?.SpellId ?? scrollItem.ConsumableSpellName;
            result.FailureReason = $"Spell '{spellRef}' not found in spell database.";
            return result;
        }

        int scrollSpellLevel = scrollItem.Scroll?.BaseSpellLevel ?? scrollItem.ScrollSpellLevel;
        int scrollCasterLevel = scrollItem.Scroll?.CasterLevel ?? scrollItem.ConsumableMinimumCasterLevel;
        string scrollType = scrollItem.Scroll?.TypeLabel ?? scrollItem.ScrollType ?? "Arcane";

        // Step 1: Find a matching class on the character that has this spell on its list
        string matchedClass = null;
        int characterCL = 0;

        string[] classesToCheck = scrollType == "Arcane" ? ArcaneClasses : DivineClasses;

        foreach (string cls in classesToCheck)
        {
            int classLevel = stats.GetClassLevel(cls);
            if (classLevel <= 0) continue;

            int spellLevelForClass = spell.GetSpellLevelFor(cls);
            if (spellLevelForClass < 0) continue;

            // Character has this class AND the spell is on that class's list
            matchedClass = cls;
            characterCL = classLevel; // Class level = caster level for full casters
            // Paladins/Rangers: CL = class level - 3 (min 1) per PHB
            if (string.Equals(cls, "Paladin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cls, "Ranger", StringComparison.OrdinalIgnoreCase))
            {
                characterCL = Mathf.Max(1, classLevel - 3);
            }
            break;
        }

        // ── Magic Domain check (D&D 3.5e PHB) ──
        // A cleric with the Magic domain can use arcane spell completion items (scrolls)
        // as if they were a wizard of half their cleric level. This check runs if no
        // normal class match was found and the scroll is arcane.
        bool usedMagicDomain = false;
        if (matchedClass == null && scrollType == "Arcane" && stats.HasMagicDomain)
        {
            int effectiveWizLevel = stats.MagicDomainEffectiveWizardLevel;
            if (effectiveWizLevel > 0)
            {
                // Magic domain treats the cleric as a wizard — check if spell is on wizard list
                int wizSpellLevel = spell.GetSpellLevelFor("Wizard");
                if (wizSpellLevel >= 0)
                {
                    matchedClass = "Wizard (Magic Domain)";
                    characterCL = effectiveWizLevel;
                    usedMagicDomain = true;
                }
            }
        }

        if (matchedClass == null)
        {
            // Check if Use Magic Device can help
            int umdBonus = stats.GetSkillBonus("Use Magic Device");
            if (umdBonus > 0)
            {
                int umdDC = 20 + scrollCasterLevel;
                result.CanUse = true;
                result.UsedUMD = true;
                result.NeedsCasterLevelCheck = false; // UMD handles everything
                result.CasterLevelCheckDC = umdDC;
                result.MatchedClassName = "Use Magic Device";
                result.CharacterCasterLevel = umdBonus; // Store UMD bonus for the check
                return result;
            }

            string magicDomainHint = stats.HasMagicDomain
                ? " (Magic domain did not match — spell may not be on the Wizard list)"
                : "";
            result.FailureReason = $"{stats.CharacterName} cannot use this {scrollType} scroll: " +
                                   $"the spell is not on any of their class spell lists.{magicDomainHint} " +
                                   $"(Requires: {string.Join("/", classesToCheck)} with this spell, or Use Magic Device skill)";
            return result;
        }

        // Step 2: Check requisite ability score (10 + spell level)
        // For Magic domain using arcane scrolls, the casting stat is INT (wizard's stat)
        int requiredAbilityScore = 10 + scrollSpellLevel;
        string effectiveClass = usedMagicDomain ? "Wizard" : matchedClass;
        int actualAbilityScore = GetAbilityScore(stats, effectiveClass);

        if (actualAbilityScore < requiredAbilityScore)
        {
            string abilityName = CastingAbility.TryGetValue(effectiveClass, out string ab) ? ab : "???";

            // Check UMD as fallback
            int umdBonus = stats.GetSkillBonus("Use Magic Device");
            if (umdBonus > 0)
            {
                int umdDC = 20 + scrollCasterLevel;
                result.CanUse = true;
                result.UsedUMD = true;
                result.NeedsCasterLevelCheck = false;
                result.CasterLevelCheckDC = umdDC;
                result.MatchedClassName = "Use Magic Device";
                result.CharacterCasterLevel = umdBonus;
                return result;
            }

            string domainNote = usedMagicDomain
                ? $" (Magic domain grants wizard access, but requires INT for arcane scrolls)"
                : "";
            result.FailureReason = $"{stats.CharacterName} needs {abilityName} ≥ {requiredAbilityScore} to use this scroll " +
                                   $"(current: {actualAbilityScore}).{domainNote} " +
                                   $"A {effectiveClass} needs {abilityName} {requiredAbilityScore} to cast a level-{scrollSpellLevel} spell.";
            return result;
        }

        // Step 3: Determine if caster level check is needed
        result.CanUse = true;
        result.MatchedClassName = matchedClass;
        result.CharacterCasterLevel = characterCL;
        result.UsedMagicDomain = usedMagicDomain;

        if (characterCL < scrollCasterLevel)
        {
            result.NeedsCasterLevelCheck = true;
            result.CasterLevelCheckDC = scrollCasterLevel + 1;
        }

        return result;
    }

    /// <summary>
    /// Perform a caster level check for a scroll whose CL exceeds the character's CL.
    /// D&D 3.5e DMG: d20 + character CL vs DC (scroll CL + 1). Nat 1 auto-fails.
    /// On failure: DC 5 Wisdom check or mishap (1d6 per spell level damage).
    /// </summary>
    public static CasterLevelCheckResult PerformCasterLevelCheck(
        CharacterController character, ItemData scrollItem, int characterCasterLevel)
    {
        var result = new CasterLevelCheckResult();
        int scrollCL = scrollItem.ConsumableMinimumCasterLevel;
        result.DC = scrollCL + 1;

        int d20 = DiceRoller.D20();
        result.D20Roll = d20;
        result.TotalCheck = d20 + characterCasterLevel;

        // Natural 1 is automatic failure
        if (d20 == 1 || result.TotalCheck < result.DC)
        {
            result.Success = false;

            // Wisdom check DC 5 to avoid mishap
            int wisMod = character != null && character.Stats != null ? character.Stats.WISMod : 0;
            int wisCheck = DiceRoller.D20() + wisMod;

            if (wisCheck >= 5)
            {
                // No mishap — scroll is preserved
                result.Mishap = false;
                result.Summary = $"Caster level check failed (d20={d20}+{characterCasterLevel}={result.TotalCheck} vs DC {result.DC}). " +
                                 $"Wisdom check passed (DC 5) — scroll is preserved but spell does not activate.";
            }
            else
            {
                // Mishap! 1d6 per spell level damage
                result.Mishap = true;
                int spellLevel = Mathf.Max(1, scrollItem.ScrollSpellLevel);
                int mishapDamage = 0;
                for (int i = 0; i < spellLevel; i++)
                    mishapDamage += DiceRoller.D6();

                result.MishapDamage = mishapDamage;
                result.Summary = $"Caster level check failed (d20={d20}+{characterCasterLevel}={result.TotalCheck} vs DC {result.DC}). " +
                                 $"Wisdom check failed (DC 5) — SCROLL MISHAP! Uncontrolled magical energy deals {mishapDamage} damage!";
            }
        }
        else
        {
            result.Success = true;
            result.Summary = $"Caster level check passed (d20={d20}+{characterCasterLevel}={result.TotalCheck} vs DC {result.DC}).";
        }

        return result;
    }

    /// <summary>
    /// Perform a Use Magic Device check to activate a scroll.
    /// DC = 20 + scroll's caster level.
    /// </summary>
    public static bool PerformUMDCheck(CharacterController character, ItemData scrollItem, out string summary)
    {
        summary = "";
        if (character == null || character.Stats == null || scrollItem == null)
        {
            summary = "Invalid character or scroll.";
            return false;
        }

        int umdBonus = character.Stats.GetSkillBonus("Use Magic Device");
        int dc = 20 + scrollItem.ConsumableMinimumCasterLevel;
        int d20 = DiceRoller.D20();
        int total = d20 + umdBonus;

        // Natural 1 on UMD is not auto-fail (UMD is a skill, not a save),
        // but rolling a natural 1 can't take 10 for an hour per SRD.
        if (total >= dc)
        {
            summary = $"Use Magic Device check passed (d20={d20}+{umdBonus}={total} vs DC {dc}).";
            return true;
        }
        else
        {
            summary = $"Use Magic Device check failed (d20={d20}+{umdBonus}={total} vs DC {dc}). " +
                       "Cannot activate this scroll via UMD.";
            return false;
        }
    }

    // ── Internal helpers ──

    /// <summary>Get the relevant ability score for a spellcasting class.</summary>
    private static int GetAbilityScore(CharacterStats stats, string className)
    {
        if (stats == null) return 0;

        if (!CastingAbility.TryGetValue(className, out string ability))
            return 10; // Default if class unknown

        switch (ability)
        {
            case "INT": return stats.INT;
            case "WIS": return stats.WIS;
            case "CHA": return stats.CHA;
            default: return 10;
        }
    }

    /// <summary>Check if character's class is arcane.</summary>
    public static bool IsCharacterArcaneCaster(CharacterStats stats)
    {
        if (stats == null) return false;
        foreach (string cls in ArcaneClasses)
        {
            if (stats.GetClassLevel(cls) > 0)
                return true;
        }
        return false;
    }

    /// <summary>Check if character's class is divine.</summary>
    public static bool IsCharacterDivineCaster(CharacterStats stats)
    {
        if (stats == null) return false;
        foreach (string cls in DivineClasses)
        {
            if (stats.GetClassLevel(cls) > 0)
                return true;
        }
        return false;
    }
}
