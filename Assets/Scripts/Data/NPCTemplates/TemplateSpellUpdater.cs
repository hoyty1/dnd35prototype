using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-updates NPC spell lists when new spells are added to SpellDatabase.
///
/// Template NPCs reference spells that may not be implemented yet.
/// When new spells are added (via SpellDatabase registration), this updater
/// can re-validate all active NPCs and add newly available spells to their
/// prepared/known lists.
///
/// Usage:
///   // After adding new spells to SpellDatabase:
///   TemplateSpellUpdater.UpdateAllActiveNPCs();
///
///   // To check a single NPC:
///   TemplateSpellUpdater.UpdateNPC(npcDefinition);
/// </summary>
public static class TemplateSpellUpdater
{
    /// <summary>
    /// Update a single NPCDefinition's spell lists against the current SpellDatabase.
    /// Re-validates the template's spells and adds any newly implemented ones.
    /// Returns the number of new spells added.
    /// </summary>
    public static int UpdateNPC(NPCDefinition def, NPCTemplate sourceTemplate)
    {
        if (def == null || sourceTemplate == null || sourceTemplate.Spellcasting == null)
            return 0;

        // Collect all template spells
        List<string> allTemplateSpells = new List<string>();
        if (sourceTemplate.Spellcasting.SpellsPrepared != null)
        {
            foreach (var kvp in sourceTemplate.Spellcasting.SpellsPrepared)
            {
                if (kvp.Value != null)
                    allTemplateSpells.AddRange(kvp.Value);
            }
        }

        // Get currently implemented spells
        List<string> implementedSpells = TemplateSpellValidator.GetImplementedSpells(allTemplateSpells);

        // Count new spells (ones not already in the definition)
        int newSpellCount = 0;
        HashSet<string> existingSpells = new HashSet<string>(def.PreparedSpellSlotIds);

        foreach (string spellId in implementedSpells)
        {
            if (!existingSpells.Contains(spellId))
            {
                def.PreparedSpellSlotIds.Add(spellId);
                newSpellCount++;

                // Also add to known spells if not present
                if (!def.KnownSpellIds.Contains(spellId))
                    def.KnownSpellIds.Add(spellId);
            }
        }

        if (newSpellCount > 0)
        {
            Debug.Log($"[TemplateSpellUpdater] Updated {def.Name}: {newSpellCount} new spells added " +
                      $"(total: {def.PreparedSpellSlotIds.Count} prepared, {def.KnownSpellIds.Count} known)");
        }

        return newSpellCount;
    }

    /// <summary>
    /// Update all active NPC CharacterControllers in the scene.
    /// Finds NPCs with SourceTemplateId set and re-validates their spells.
    /// Returns the total number of new spells added across all NPCs.
    /// </summary>
    public static int UpdateAllActiveNPCs()
    {
        SpellDatabase.Init();
        NPCTemplateDatabase.Init();

        CharacterController[] allCharacters = Object.FindObjectsOfType<CharacterController>();
        int totalNewSpells = 0;
        int updatedNPCs = 0;

        foreach (CharacterController character in allCharacters)
        {
            if (character == null || character.Stats == null) continue;

            // Check if this NPC has a source template
            string templateId = GetSourceTemplateId(character);
            if (string.IsNullOrEmpty(templateId)) continue;

            // Find the source template
            NPCTemplate template = FindTemplateById(templateId);
            if (template == null) continue;

            // Update the character's SpellcastingComponent
            var spellcasting = character.GetComponent<SpellcastingComponent>();
            if (spellcasting == null) continue;

            int newSpells = UpdateRuntimeSpellcasting(character, spellcasting, template);
            if (newSpells > 0)
            {
                totalNewSpells += newSpells;
                updatedNPCs++;
            }
        }

        Debug.Log($"[TemplateSpellUpdater] Updated {updatedNPCs} NPCs with {totalNewSpells} total new spells");
        return totalNewSpells;
    }

    /// <summary>
    /// Update spells on a live SpellcastingComponent from a template.
    /// Returns the number of new spells added.
    /// </summary>
    private static int UpdateRuntimeSpellcasting(
        CharacterController character,
        SpellcastingComponent spellcasting,
        NPCTemplate template)
    {
        if (template.Spellcasting == null) return 0;

        // Collect all template spells
        List<string> allTemplateSpells = new List<string>();
        if (template.Spellcasting.SpellsPrepared != null)
        {
            foreach (var kvp in template.Spellcasting.SpellsPrepared)
            {
                if (kvp.Value != null)
                    allTemplateSpells.AddRange(kvp.Value);
            }
        }

        List<string> implementedSpells = TemplateSpellValidator.GetImplementedSpells(allTemplateSpells);

        // Track existing known spell IDs
        HashSet<string> existingSpellIds = new HashSet<string>();
        foreach (SpellData known in spellcasting.KnownSpells)
        {
            if (known != null)
                existingSpellIds.Add(known.SpellId);
        }

        int newCount = 0;
        foreach (string spellId in implementedSpells)
        {
            if (!existingSpellIds.Contains(spellId))
            {
                SpellData spell = SpellDatabase.GetSpell(spellId);
                if (spell != null)
                {
                    spellcasting.KnownSpells.Add(spell);
                    newCount++;
                }
            }
        }

        // Update prepared spell slot IDs
        if (spellcasting.PreparedSpellSlotIds != null)
        {
            HashSet<string> existingPrepared = new HashSet<string>(spellcasting.PreparedSpellSlotIds);
            foreach (string spellId in implementedSpells)
            {
                if (!existingPrepared.Contains(spellId))
                    spellcasting.PreparedSpellSlotIds.Add(spellId);
            }
        }

        return newCount;
    }

    /// <summary>
    /// Get the source template ID from a CharacterController.
    /// Checks the NPC definition's source template field.
    /// </summary>
    private static string GetSourceTemplateId(CharacterController character)
    {
        if (character == null || character.Stats == null) return null;

        // Use the SourceTemplateId we track on NPCDefinition
        // Convention: "template_{class}_{level}" matches NPCTemplate key
        string className = character.Stats.CharacterClass;
        int level = character.Stats.Level;

        if (string.IsNullOrEmpty(className)) return null;

        return $"{className}_{level}";
    }

    /// <summary>
    /// Find a template by its class_level ID.
    /// </summary>
    private static NPCTemplate FindTemplateById(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return null;

        // Parse "ClassName_Level" format
        int lastUnderscore = templateId.LastIndexOf('_');
        if (lastUnderscore <= 0 || lastUnderscore >= templateId.Length - 1) return null;

        string className = templateId.Substring(0, lastUnderscore);
        string levelStr = templateId.Substring(lastUnderscore + 1);

        if (int.TryParse(levelStr, out int level))
        {
            return NPCTemplateDatabase.GetTemplate(className, level);
        }

        return null;
    }

    /// <summary>
    /// Get a report of all template spells and their implementation status.
    /// Useful for tracking spell implementation progress.
    /// </summary>
    public static string GetImplementationReport()
    {
        NPCTemplateDatabase.Init();
        SpellDatabase.Init();

        HashSet<string> allSpellIds = new HashSet<string>();
        HashSet<string> implementedIds = new HashSet<string>();
        HashSet<string> unimplementedIds = new HashSet<string>();

        List<NPCTemplate> allTemplates = NPCTemplateDatabase.GetAllTemplates();
        foreach (NPCTemplate template in allTemplates)
        {
            if (template.Spellcasting == null || template.Spellcasting.SpellsPrepared == null) continue;

            foreach (var kvp in template.Spellcasting.SpellsPrepared)
            {
                if (kvp.Value == null) continue;
                foreach (string spellId in kvp.Value)
                {
                    allSpellIds.Add(spellId);
                    SpellData spell = SpellDatabase.GetSpell(spellId);
                    if (spell != null && !spell.IsPlaceholder)
                        implementedIds.Add(spellId);
                    else
                        unimplementedIds.Add(spellId);
                }
            }
        }

        return $"Template Spell Implementation Report:\n" +
               $"  Total unique spells referenced: {allSpellIds.Count}\n" +
               $"  Implemented: {implementedIds.Count}\n" +
               $"  Unimplemented/Placeholder: {unimplementedIds.Count}\n" +
               $"  Coverage: {(allSpellIds.Count > 0 ? (implementedIds.Count * 100 / allSpellIds.Count) : 100)}%";
    }
}
