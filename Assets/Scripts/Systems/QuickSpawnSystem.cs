using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quick NPC spawning system (D&D 3.5e DMG Chapter 4).
/// Spawns NPCs from pre-built templates or applies class levels to existing creatures.
///
/// Usage:
///   var npc = QuickSpawnSystem.SpawnNPC("Fighter", 10);
///   var ogre = QuickSpawnSystem.ApplyClassToCreature(ogreDef, "Barbarian", 3);
/// </summary>
public static class QuickSpawnSystem
{
    /// <summary>
    /// Spawn an NPC from a template and return the NPCDefinition ready for use.
    /// Uses the NPCTemplateDatabase to find the nearest template.
    /// </summary>
    public static NPCDefinition SpawnNPC(string className, int level)
    {
        NPCTemplateDatabase.Init();
        ClassRegistry.Init();

        NPCTemplate template = NPCTemplateDatabase.GetNearestTemplate(className, level);
        if (template == null)
        {
            Debug.LogWarning($"[QuickSpawn] No template found for {className} L{level}");
            return null;
        }

        return CreateFromTemplate(template);
    }

    /// <summary>
    /// Spawn a random NPC matching a target CR.
    /// </summary>
    public static NPCDefinition SpawnNPCByCR(int targetCR)
    {
        NPCTemplateDatabase.Init();
        NPCTemplate template = NPCTemplateDatabase.GetRandomTemplateForCR(targetCR);
        if (template == null)
        {
            Debug.LogWarning($"[QuickSpawn] No template found for CR {targetCR}");
            return null;
        }

        return CreateFromTemplate(template);
    }

    /// <summary>
    /// Apply class levels to an existing creature definition.
    /// Modifies the creature's stats and recalculates CR.
    /// Returns the modified definition.
    /// </summary>
    public static NPCDefinition ApplyClassToCreature(NPCDefinition creature, string className, int levels)
    {
        if (creature == null)
        {
            Debug.LogWarning("[QuickSpawn] Cannot apply class to null creature");
            return null;
        }

        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(className);
        if (classDef == null)
        {
            Debug.LogWarning($"[QuickSpawn] Class not found: {className}");
            return creature;
        }

        // Clone to avoid modifying the original database entry
        NPCDefinition modified = creature.Clone();
        CreatureClassEngine.ApplyClassToDefinition(modified, classDef, levels);

        return modified;
    }

    /// <summary>
    /// Create an NPCDefinition from a template.
    /// Configures AI behavior, spellcasting, and consumable awareness automatically.
    /// </summary>
    public static NPCDefinition CreateFromTemplate(NPCTemplate template)
    {
        if (template == null) return null;

        string npcName = $"{template.Race} {template.ClassName} {template.Level}";

        var def = new NPCDefinition
        {
            Id = $"npc_{template.ClassName.ToLower()}_{template.Level}",
            Name = npcName,
            Description = $"A level {template.Level} {template.ClassName}.",
            ChallengeRating = template.ChallengeRating.ToString(),
            Level = template.Level,
            CharacterClass = template.ClassName,
            CreatureType = "Humanoid",
            HitDice = template.Level,
            STR = template.Strength,
            DEX = template.Dexterity,
            CON = template.Constitution,
            WIS = template.Wisdom,
            INT = template.Intelligence,
            CHA = template.Charisma,
            BAB = template.BaseAttackBonus,
            BaseSpeed = template.BaseSpeed > 0 ? template.BaseSpeed / 5 : 6, // Convert feet to cells
            BaseHitDieHP = template.HitPoints,
            Feats = new List<string>(template.Feats),
            AIBehavior = GetDefaultAI(template.ClassName)
        };

        // Set alignment
        def.CharacterAlignment = ParseAlignment(template.Alignment);

        // Add class features as special abilities
        if (template.ClassFeatures != null)
            def.SpecialAbilities = new List<string>(template.ClassFeatures);

        // Track source template for auto-updates
        def.SourceTemplateId = $"{template.ClassName}_{template.Level}";

        // Configure AI behavior, spellcasting, and consumables from template
        NPCTemplateAIConfigurator.ConfigureDefinition(def, template);

        Debug.Log($"[QuickSpawn] Created {npcName}: CR {template.ChallengeRating}, HP {template.HitPoints}, " +
                  $"AC {template.ArmorClass}, BAB +{template.BaseAttackBonus}");

        return def;
    }

    /// <summary>
    /// Recalculate CR for a creature based on its current state.
    /// </summary>
    public static int RecalculateCR(NPCDefinition def)
    {
        if (def == null) return 0;
        return Mathf.Max(0, Mathf.RoundToInt(CRCalculator.CRToFloat(def.ChallengeRating)));
    }

    /// <summary>Get default AI behavior for a class.</summary>
    private static NPCAIBehavior GetDefaultAI(string className)
    {
        switch (className)
        {
            case "Fighter":
            case "Barbarian":
            case "Warrior":
            case "Paladin":
            case "Monk":
                return NPCAIBehavior.AggressiveMelee;

            case "Ranger":
                return NPCAIBehavior.RangedKiter;

            case "Rogue":
                return NPCAIBehavior.DefensiveMelee;

            case "Wizard":
            case "Sorcerer":
            case "Adept":
            case "Cleric":
            case "Druid":
            case "Bard":
                return NPCAIBehavior.RangedKiter; // Casters stay at range

            case "Aristocrat":
            case "Expert":
            case "Commoner":
            default:
                return NPCAIBehavior.AggressiveMelee;
        }
    }

    /// <summary>Parse alignment string to enum.</summary>
    private static Alignment ParseAlignment(string alignment)
    {
        if (string.IsNullOrEmpty(alignment)) return Alignment.TrueNeutral;
        switch (alignment.ToLower().Replace(" ", "").Replace("-", ""))
        {
            case "lawfulgood": return Alignment.LawfulGood;
            case "neutralgood": return Alignment.NeutralGood;
            case "chaoticgood": return Alignment.ChaoticGood;
            case "lawfulneutral": return Alignment.LawfulNeutral;
            case "trueneutral": return Alignment.TrueNeutral;
            case "chaoticneutral": return Alignment.ChaoticNeutral;
            case "lawfulevil": return Alignment.LawfulEvil;
            case "neutralevil": return Alignment.NeutralEvil;
            case "chaoticevil": return Alignment.ChaoticEvil;
            default: return Alignment.TrueNeutral;
        }
    }
}
