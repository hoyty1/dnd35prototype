using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates and filters template spells against the SpellDatabase.
/// Handles graceful degradation when spells are not yet implemented.
///
/// D&D 3.5e DMG templates reference many spells; this validator ensures
/// only implemented spells are loaded while tracking unimplemented ones
/// for future auto-update when they become available.
/// </summary>
public static class TemplateSpellValidator
{
    /// <summary>
    /// Filter a list of template spell IDs to only those implemented in SpellDatabase.
    /// Returns a new list containing only validated spell IDs.
    /// </summary>
    public static List<string> GetImplementedSpells(List<string> templateSpellIds)
    {
        if (templateSpellIds == null || templateSpellIds.Count == 0)
            return new List<string>();

        SpellDatabase.Init();
        List<string> implemented = new List<string>();

        foreach (string spellId in templateSpellIds)
        {
            if (string.IsNullOrWhiteSpace(spellId)) continue;

            SpellData spell = SpellDatabase.GetSpell(spellId);
            if (spell != null && !spell.IsPlaceholder)
            {
                implemented.Add(spellId);
            }
            else if (spell != null && spell.IsPlaceholder)
            {
                Debug.Log($"[TemplateSpellValidator] Spell '{spellId}' exists but is placeholder — skipping");
            }
            else
            {
                Debug.Log($"[TemplateSpellValidator] Spell '{spellId}' not yet implemented — will auto-update when available");
            }
        }

        return implemented;
    }

    /// <summary>
    /// Get unimplemented spells from a template spell list.
    /// Useful for tracking what still needs to be added.
    /// </summary>
    public static List<string> GetUnimplementedSpells(List<string> templateSpellIds)
    {
        if (templateSpellIds == null || templateSpellIds.Count == 0)
            return new List<string>();

        SpellDatabase.Init();
        List<string> unimplemented = new List<string>();

        foreach (string spellId in templateSpellIds)
        {
            if (string.IsNullOrWhiteSpace(spellId)) continue;

            SpellData spell = SpellDatabase.GetSpell(spellId);
            if (spell == null || spell.IsPlaceholder)
            {
                unimplemented.Add(spellId);
            }
        }

        return unimplemented;
    }

    /// <summary>
    /// Organize validated spell IDs by spell level.
    /// Returns a dictionary mapping spell level (0-9) to lists of spell IDs.
    /// </summary>
    public static Dictionary<int, List<string>> OrganizeSpellsByLevel(List<string> spellIds)
    {
        Dictionary<int, List<string>> byLevel = new Dictionary<int, List<string>>();

        if (spellIds == null) return byLevel;

        foreach (string spellId in spellIds)
        {
            SpellData spell = SpellDatabase.GetSpell(spellId);
            if (spell == null) continue;

            int level = spell.SpellLevel;
            if (!byLevel.ContainsKey(level))
                byLevel[level] = new List<string>();
            byLevel[level].Add(spellId);
        }

        return byLevel;
    }

    /// <summary>
    /// Categorize a spell by its tactical priority for AI decision-making.
    /// Uses SpellData.EffectType to map to our SpellPriority categories.
    /// </summary>
    public static SpellPriority GetSpellPriority(string spellId)
    {
        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null) return SpellPriority.Utility;

        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
                return SpellPriority.Offensive;
            case SpellEffectType.Healing:
                return SpellPriority.Healing;
            case SpellEffectType.Buff:
            case SpellEffectType.Illusion:
                return SpellPriority.Buff;
            case SpellEffectType.Control:
            case SpellEffectType.Debuff:
            case SpellEffectType.Wall:
            case SpellEffectType.Dispel:
                return SpellPriority.Defensive;
            case SpellEffectType.Summon:
                return SpellPriority.Offensive;
            case SpellEffectType.Escape:
            case SpellEffectType.Utility:
            case SpellEffectType.Divination:
            default:
                return SpellPriority.Utility;
        }
    }

    /// <summary>
    /// Organize spells by priority category.
    /// Returns a dictionary of SpellPriority → list of spell IDs.
    /// </summary>
    public static Dictionary<SpellPriority, List<string>> CategorizeSpells(List<string> spellIds)
    {
        var categorized = new Dictionary<SpellPriority, List<string>>
        {
            { SpellPriority.Offensive, new List<string>() },
            { SpellPriority.Defensive, new List<string>() },
            { SpellPriority.Healing, new List<string>() },
            { SpellPriority.Buff, new List<string>() },
            { SpellPriority.Utility, new List<string>() }
        };

        if (spellIds == null) return categorized;

        foreach (string spellId in spellIds)
        {
            SpellPriority priority = GetSpellPriority(spellId);
            categorized[priority].Add(spellId);
        }

        return categorized;
    }

    /// <summary>
    /// Get a validation summary for a template's spell list.
    /// </summary>
    public static string GetValidationSummary(List<string> templateSpellIds)
    {
        if (templateSpellIds == null || templateSpellIds.Count == 0)
            return "No spells in template";

        List<string> implemented = GetImplementedSpells(templateSpellIds);
        List<string> unimplemented = GetUnimplementedSpells(templateSpellIds);

        return $"{implemented.Count}/{templateSpellIds.Count} spells implemented" +
               (unimplemented.Count > 0 ? $" ({unimplemented.Count} pending)" : " (all available)");
    }
}

/// <summary>
/// Spell priority categories for AI tactical decision-making.
/// Maps to SpellEffectType but simplified for template AI configuration.
/// </summary>
public enum SpellPriority
{
    Offensive,   // Damage, Summon
    Defensive,   // Control, Debuff, Wall, Dispel
    Healing,     // Healing
    Buff,        // Buff, Illusion
    Utility      // Utility, Escape, Divination
}
