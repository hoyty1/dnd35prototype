using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configures AI behavior for NPCs spawned from DMG Chapter 4 templates.
///
/// This configurator works at two levels:
/// 1. NPCDefinition level — sets AI behavior, profile archetype, spell IDs, and
///    consumable tracking during template-based spawning (pure data, no MonoBehaviour).
/// 2. Runtime level — when a CharacterController is instantiated from the definition,
///    further configures SpellcastingComponent and AI profile details.
///
/// The system gracefully handles unimplemented spells by filtering them out
/// and supports auto-updating when new spells become available.
/// </summary>
public static class NPCTemplateAIConfigurator
{
    /// <summary>
    /// Configure an NPCDefinition's AI data from a template.
    /// Called during QuickSpawnSystem.CreateFromTemplate().
    /// Sets AIBehavior, AIProfileArchetype, spell lists, and consumable data.
    /// </summary>
    public static void ConfigureDefinition(NPCDefinition def, NPCTemplate template)
    {
        if (def == null || template == null) return;

        // 1. Set AI behavior based on class
        def.AIBehavior = GetBehaviorForClass(template.ClassName);

        // 2. Set AI profile archetype based on class
        def.AIProfileArchetype = GetProfileArchetypeForClass(template.ClassName);

        // 3. Configure spellcasting if template has spells
        if (template.Spellcasting != null)
        {
            ConfigureSpellcasting(def, template);
        }

        // 4. Configure consumable awareness
        if (template.Equipment != null && template.Equipment.Count > 0)
        {
            ConfigureConsumables(def, template);
        }

        Debug.Log($"[AIConfigurator] Configured AI for {template.ClassName} L{template.Level}: " +
                  $"Behavior={def.AIBehavior}, Profile={def.AIProfileArchetype}, " +
                  $"Spells={def.PreparedSpellSlotIds.Count}, Known={def.KnownSpellIds.Count}");
    }

    /// <summary>
    /// Configure AI for a runtime CharacterController that was spawned from a template.
    /// Called after the character is instantiated in the scene.
    /// </summary>
    public static void ConfigureRuntime(CharacterController character, NPCTemplate template)
    {
        if (character == null || template == null) return;

        // Configure SpellcastingComponent if present
        var spellcasting = character.Spellcasting;
        if (spellcasting != null && template.Spellcasting != null)
        {
            ConfigureRuntimeSpellcasting(character, spellcasting, template);
        }

        Debug.Log($"[AIConfigurator] Runtime AI configured for {character.Stats?.CharacterName ?? template.ClassName}");
    }

    // ==================== DEFINITION-LEVEL CONFIGURATION ====================

    /// <summary>
    /// Configure spell IDs on the NPCDefinition from template spellcasting data.
    /// Validates spells against SpellDatabase and categorizes them.
    /// </summary>
    private static void ConfigureSpellcasting(NPCDefinition def, NPCTemplate template)
    {
        if (template.Spellcasting == null) return;

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

        // Validate against SpellDatabase — only use implemented spells
        List<string> implementedSpells = TemplateSpellValidator.GetImplementedSpells(allTemplateSpells);
        List<string> unimplemented = TemplateSpellValidator.GetUnimplementedSpells(allTemplateSpells);

        // Set prepared spells (for prepared casters like Wizard, Cleric, Adept)
        def.PreparedSpellSlotIds.Clear();
        def.PreparedSpellSlotIds.AddRange(implementedSpells);

        // Set known spells (for spontaneous casters like Sorcerer, Bard, and all casters for AI reference)
        def.KnownSpellIds.Clear();
        def.KnownSpellIds.AddRange(implementedSpells);

        // Override AI behavior for spellcasters
        if (implementedSpells.Count > 0)
        {
            def.AIBehavior = NPCAIBehavior.RangedKiter; // Casters stay at range
        }

        // Log spell validation summary
        string summary = TemplateSpellValidator.GetValidationSummary(allTemplateSpells);
        Debug.Log($"[AIConfigurator] Spellcasting for {def.CharacterClass}: {summary}");

        if (unimplemented.Count > 0)
        {
            Debug.Log($"[AIConfigurator] Unimplemented spells for {def.CharacterClass}: " +
                      string.Join(", ", unimplemented));
        }
    }

    /// <summary>
    /// Mark consumable items in the NPC's backpack for AI usage.
    /// Adds potion/scroll/wand item IDs to BackpackItemIds so the AI
    /// can access them during combat.
    /// </summary>
    private static void ConfigureConsumables(NPCDefinition def, NPCTemplate template)
    {
        if (template.Equipment == null) return;

        foreach (EquipmentItem item in template.Equipment)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemName)) continue;

            string lower = item.ItemName.ToLower();

            // Classify consumables and add to backpack
            if (lower.Contains("potion") || lower.Contains("scroll") || lower.Contains("wand"))
            {
                // Map template item name to item ID format
                string itemId = MapTemplateItemToId(item.ItemName);
                if (!string.IsNullOrEmpty(itemId) && !def.BackpackItemIds.Contains(itemId))
                {
                    def.BackpackItemIds.Add(itemId);
                }
            }
        }
    }

    // ==================== RUNTIME CONFIGURATION ====================

    /// <summary>
    /// Configure SpellcastingComponent on a live character from template data.
    /// Adds known spells and prepares spell slots.
    /// </summary>
    private static void ConfigureRuntimeSpellcasting(
        CharacterController character,
        SpellcastingComponent spellcasting,
        NPCTemplate template)
    {
        if (template.Spellcasting == null) return;

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

        // Validate and add as known spells
        List<string> implementedSpells = TemplateSpellValidator.GetImplementedSpells(allTemplateSpells);

        foreach (string spellId in implementedSpells)
        {
            SpellData spell = SpellDatabase.GetSpell(spellId);
            if (spell != null && !spellcasting.KnownSpells.Contains(spell))
            {
                spellcasting.KnownSpells.Add(spell);
            }
        }

        // Set prepared spell slot IDs for the component
        if (spellcasting.PreparedSpellSlotIds == null)
            spellcasting.PreparedSpellSlotIds = new List<string>();
        spellcasting.PreparedSpellSlotIds.Clear();
        spellcasting.PreparedSpellSlotIds.AddRange(implementedSpells);

        Debug.Log($"[AIConfigurator] Runtime spellcasting: {implementedSpells.Count} spells loaded for " +
                  $"{character.Stats?.CharacterName ?? "NPC"}");
    }

    // ==================== BEHAVIOR MAPPING ====================

    /// <summary>
    /// Get the appropriate NPCAIBehavior for a class.
    /// D&D 3.5e DMG class archetypes map to combat behaviors.
    /// </summary>
    public static NPCAIBehavior GetBehaviorForClass(string className)
    {
        if (string.IsNullOrEmpty(className)) return NPCAIBehavior.AggressiveMelee;

        switch (className)
        {
            // Melee combatants — close range, direct pressure
            case "Fighter":
            case "Barbarian":
            case "Paladin":
            case "Monk":
            case "Warrior":
                return NPCAIBehavior.AggressiveMelee;

            // Ranged/caster — stay at distance, use ranged attacks or spells
            case "Ranger":
            case "Wizard":
            case "Sorcerer":
            case "Adept":
            case "Bard":
                return NPCAIBehavior.RangedKiter;

            // Support casters — range but may close for touch spells
            case "Cleric":
            case "Druid":
                return NPCAIBehavior.RangedKiter;

            // Tactical — defensive positioning
            case "Rogue":
                return NPCAIBehavior.DefensiveMelee;

            // NPC support classes — basic melee
            case "Aristocrat":
            case "Expert":
            case "Commoner":
            default:
                return NPCAIBehavior.AggressiveMelee;
        }
    }

    /// <summary>
    /// Get the appropriate AIProfileArchetype for a class.
    /// Used by the AI system to select the right SpellcasterAIProfile variant.
    /// </summary>
    public static NPCAIProfileArchetype GetProfileArchetypeForClass(string className)
    {
        if (string.IsNullOrEmpty(className)) return NPCAIProfileArchetype.Humanoid;

        switch (className)
        {
            // Full casters — specialized AI profiles
            case "Wizard":
                return NPCAIProfileArchetype.Evoker;
            case "Sorcerer":
                return NPCAIProfileArchetype.Spellcaster;
            case "Cleric":
            case "Adept":
                return NPCAIProfileArchetype.Healer;
            case "Druid":
                return NPCAIProfileArchetype.Spellcaster;
            case "Bard":
                return NPCAIProfileArchetype.Spellcaster;

            // Partial casters — humanoid with ranged preference
            case "Ranger":
            case "Paladin":
                return NPCAIProfileArchetype.Ranged;

            // Melee fighters — standard humanoid
            case "Fighter":
            case "Barbarian":
            case "Warrior":
            case "Monk":
                return NPCAIProfileArchetype.Humanoid;

            // Rogue — humanoid (relies on flanking/sneak attack)
            case "Rogue":
                return NPCAIProfileArchetype.Humanoid;

            // NPC classes — basic humanoid
            case "Aristocrat":
            case "Expert":
            case "Commoner":
            default:
                return NPCAIProfileArchetype.Humanoid;
        }
    }

    /// <summary>
    /// Map a template equipment item name to an item database ID.
    /// Template items use display names; this converts to the ID format.
    /// </summary>
    private static string MapTemplateItemToId(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;

        // Convert "Potion of Cure Light Wounds" → "potion_cure_light_wounds"
        string id = itemName.ToLower()
            .Replace("potion of ", "potion_")
            .Replace("scroll of ", "scroll_")
            .Replace("wand of ", "wand_")
            .Replace(" ", "_")
            .Replace("'", "")
            .Replace("+", "plus_");

        return id;
    }

    /// <summary>
    /// Get a summary of AI configuration for debugging.
    /// </summary>
    public static string GetConfigurationSummary(NPCDefinition def, NPCTemplate template)
    {
        if (def == null || template == null) return "No data";

        string spellInfo = template.Spellcasting != null
            ? $", Spells: {def.PreparedSpellSlotIds.Count} prepared, {def.KnownSpellIds.Count} known"
            : ", No spellcasting";

        string consumableInfo = def.BackpackItemIds.Count > 0
            ? $", Consumables: {def.BackpackItemIds.Count} items"
            : "";

        return $"AI: {def.AIBehavior}, Profile: {def.AIProfileArchetype}{spellInfo}{consumableInfo}";
    }
}
