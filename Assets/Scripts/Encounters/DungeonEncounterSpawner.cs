using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dynamic encounter spawn system for dungeon encounters (Phase 2).
///
/// Takes EncounterDefinitions and produces ready-to-use NPCDefinitions with
/// class levels and creature templates applied dynamically at spawn time.
///
/// Architecture:
///   EncounterDefinition → DungeonEncounterSpawner → List&lt;NPCDefinition&gt;
///   Then fed into GameManager.SetupEnemyEncounter via temporary registration.
///
/// Key design decisions:
///   - Base creatures are NEVER modified in NPCDatabase; always cloned first.
///   - Class levels applied via CreatureClassEngine (existing system).
///   - Creature templates applied via CreatureTemplateRegistry (existing system).
///   - Spawned definitions are registered with unique IDs for the encounter system.
///   - Supports the existing combat pipeline (SetupEnemyEncounter takes string IDs).
///
/// Usage:
///   var encounter = new EncounterDefinition("Lizardfolk Raiding Party")
///       .AddCreatureWithClass("lizardfolk", "Druid", 5, count: 1)
///       .AddCreature("lizardfolk", count: 3);
///   var result = DungeonEncounterSpawner.PrepareEncounter(encounter);
///   // result.EnemyIds → ready for SetupEnemyEncounter
///   // result.Definitions → the modified NPCDefinitions
/// </summary>
public static class DungeonEncounterSpawner
{
    // Counter for generating unique spawn IDs within a session
    private static int _spawnCounter = 0;

    /// <summary>
    /// Result of preparing an encounter. Contains everything needed to
    /// feed into the existing combat system.
    /// </summary>
    public class SpawnResult
    {
        /// <summary>
        /// Flat list of NPC IDs ready for SetupEnemyEncounter.
        /// These are registered in NPCDatabase as temporary spawn entries.
        /// </summary>
        public List<string> EnemyIds = new List<string>();

        /// <summary>
        /// The modified NPCDefinitions (cloned + class levels + templates applied).
        /// Indexed 1:1 with EnemyIds.
        /// </summary>
        public List<NPCDefinition> Definitions = new List<NPCDefinition>();

        /// <summary>Source encounter definition for reference.</summary>
        public EncounterDefinition Source;

        /// <summary>Total creature count.</summary>
        public int Count => EnemyIds.Count;

        /// <summary>Whether all creatures were successfully resolved.</summary>
        public bool IsValid => EnemyIds.Count > 0 && EnemyIds.Count == Definitions.Count;

        /// <summary>Any warnings generated during spawn preparation.</summary>
        public List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Prepare an encounter from a definition. Resolves all base creatures,
    /// applies class levels and templates, and registers temporary NPC entries.
    ///
    /// This does NOT instantiate GameObjects — it prepares the data layer.
    /// The actual GameObject spawning happens via SetupEnemyEncounter.
    /// </summary>
    public static SpawnResult PrepareEncounter(EncounterDefinition encounter)
    {
        if (encounter == null)
        {
            Debug.LogError("[DungeonEncounterSpawner] Cannot prepare null encounter.");
            return new SpawnResult();
        }

        // Ensure databases are initialized
        NPCDatabase.Init();
        ClassRegistry.Init();

        SpawnResult result = new SpawnResult { Source = encounter };

        Debug.Log($"[DungeonEncounterSpawner] Preparing encounter: {encounter.Name ?? "unnamed"} " +
                  $"({encounter.Entries.Count} entries, {encounter.TotalCreatureCount} total creatures)");

        for (int entryIdx = 0; entryIdx < encounter.Entries.Count; entryIdx++)
        {
            EncounterCreatureEntry entry = encounter.Entries[entryIdx];
            if (entry == null || string.IsNullOrEmpty(entry.BaseCreatureId))
            {
                result.Warnings.Add($"Entry {entryIdx}: null or missing BaseCreatureId");
                continue;
            }

            // Resolve base creature from database
            NPCDefinition baseDef = NPCDatabase.Get(entry.BaseCreatureId);
            if (baseDef == null)
            {
                string warning = $"Entry {entryIdx}: base creature '{entry.BaseCreatureId}' not found in NPCDatabase";
                result.Warnings.Add(warning);
                Debug.LogWarning($"[DungeonEncounterSpawner] {warning}");
                continue;
            }

            int count = Mathf.Max(1, entry.Count);
            for (int spawnIdx = 0; spawnIdx < count; spawnIdx++)
            {
                NPCDefinition spawnDef = BuildSpawnDefinition(baseDef, entry, entryIdx, spawnIdx, result.Warnings);
                if (spawnDef == null)
                    continue;

                // Register with a unique temporary ID so SetupEnemyEncounter can find it
                string spawnId = GenerateSpawnId(entry, spawnIdx);
                spawnDef.Id = spawnId;

                // Give a descriptive name
                if (count > 1)
                    spawnDef.Name = $"{spawnDef.Name} #{spawnIdx + 1}";

                // Register in NPCDatabase (overwrites if ID already exists from a previous encounter)
                NPCDatabase.RegisterExternal(spawnDef);

                result.EnemyIds.Add(spawnId);
                result.Definitions.Add(spawnDef);

                Debug.Log($"[DungeonEncounterSpawner] Prepared: {spawnDef.Name} (ID: {spawnId}) " +
                          $"HD {spawnDef.HitDice}, HP {spawnDef.BaseHitDieHP}, CR {spawnDef.ChallengeRating}");
            }
        }

        if (result.Warnings.Count > 0)
        {
            Debug.LogWarning($"[DungeonEncounterSpawner] {result.Warnings.Count} warning(s) during encounter preparation:");
            for (int i = 0; i < result.Warnings.Count; i++)
                Debug.LogWarning($"  - {result.Warnings[i]}");
        }

        Debug.Log($"[DungeonEncounterSpawner] Encounter ready: {result.Count} creatures prepared. " +
                  $"IDs: [{string.Join(", ", result.EnemyIds)}]");

        return result;
    }

    /// <summary>
    /// Build a single spawn definition from a base creature + entry modifiers.
    /// Clones the base, applies class levels, then applies creature templates.
    /// </summary>
    private static NPCDefinition BuildSpawnDefinition(
        NPCDefinition baseDef,
        EncounterCreatureEntry entry,
        int entryIdx,
        int spawnIdx,
        List<string> warnings)
    {
        // Step 1: Clone the base creature (never modify the database original)
        NPCDefinition def = baseDef.Clone();

        // Step 2: Apply class levels if specified
        if (entry.HasClassLevels)
        {
            ICharacterClass classDef = ClassRegistry.GetClass(entry.TemplateClass);
            if (classDef != null)
            {
                CreatureClassEngine.ApplyClassToDefinition(def, classDef, entry.TemplateLevel);

                // Update display name to reflect class
                def.Name = $"{baseDef.Name} {entry.TemplateClass} {entry.TemplateLevel}";

                // Update AI behavior for class-leveled creatures
                UpdateAIForClass(def, entry.TemplateClass);

                Debug.Log($"[DungeonEncounterSpawner] Applied {entry.TemplateClass} {entry.TemplateLevel} " +
                          $"to {baseDef.Name}: HD {baseDef.HitDice}→{def.HitDice}, " +
                          $"HP {baseDef.BaseHitDieHP}→{def.BaseHitDieHP}, CR {def.ChallengeRating}");
            }
            else
            {
                string warning = $"Entry {entryIdx}: class '{entry.TemplateClass}' not found in ClassRegistry";
                warnings.Add(warning);
                Debug.LogWarning($"[DungeonEncounterSpawner] {warning}");
            }
        }

        // Step 3: Apply creature templates (celestial, fiendish, etc.)
        if (entry.HasCreatureTemplates)
        {
            if (def.AppliedTemplateIds == null)
                def.AppliedTemplateIds = new List<string>();

            for (int i = 0; i < entry.CreatureTemplateIds.Count; i++)
            {
                string templateId = entry.CreatureTemplateIds[i];
                if (!string.IsNullOrEmpty(templateId))
                {
                    bool alreadyApplied = false;
                    for (int j = 0; j < def.AppliedTemplateIds.Count; j++)
                    {
                        if (string.Equals(def.AppliedTemplateIds[j], templateId, StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyApplied = true;
                            break;
                        }
                    }
                    if (!alreadyApplied)
                        def.AppliedTemplateIds.Add(templateId);
                }
            }

            // Apply via the existing template system
            def = CreatureTemplateRegistry.ApplyTemplatesClone(def);
        }

        return def;
    }

    /// <summary>
    /// Update AI behavior when class levels are applied to a creature.
    /// Caster classes get ranged AI, melee classes keep or upgrade existing behavior.
    /// </summary>
    private static void UpdateAIForClass(NPCDefinition def, string className)
    {
        if (def == null || string.IsNullOrEmpty(className))
            return;

        switch (className)
        {
            case "Wizard":
            case "Sorcerer":
                def.AIBehavior = NPCAIBehavior.RangedKiter;
                def.AIProfileArchetype = NPCAIProfileArchetype.Evoker;
                break;

            case "Cleric":
                def.AIBehavior = NPCAIBehavior.RangedKiter;
                def.AIProfileArchetype = NPCAIProfileArchetype.Healer;
                break;

            case "Druid":
                def.AIBehavior = NPCAIBehavior.RangedKiter;
                def.AIProfileArchetype = NPCAIProfileArchetype.Spellcaster;
                break;

            case "Bard":
                def.AIBehavior = NPCAIBehavior.RangedKiter;
                def.AIProfileArchetype = NPCAIProfileArchetype.Spellcaster;
                break;

            case "Adept":
                def.AIBehavior = NPCAIBehavior.RangedKiter;
                def.AIProfileArchetype = NPCAIProfileArchetype.Spellcaster;
                break;

            case "Ranger":
                def.AIBehavior = NPCAIBehavior.RangedKiter;
                def.AIProfileArchetype = NPCAIProfileArchetype.Ranged;
                break;

            case "Barbarian":
                def.AIBehavior = NPCAIBehavior.AggressiveMelee;
                def.AIProfileArchetype = NPCAIProfileArchetype.Berserk;
                break;

            case "Fighter":
            case "Paladin":
            case "Warrior":
                def.AIBehavior = NPCAIBehavior.AggressiveMelee;
                if (def.AIProfileArchetype == NPCAIProfileArchetype.None
                    || def.AIProfileArchetype == NPCAIProfileArchetype.Animal)
                    def.AIProfileArchetype = NPCAIProfileArchetype.Humanoid;
                break;

            case "Rogue":
                def.AIBehavior = NPCAIBehavior.DefensiveMelee;
                if (def.AIProfileArchetype == NPCAIProfileArchetype.None
                    || def.AIProfileArchetype == NPCAIProfileArchetype.Animal)
                    def.AIProfileArchetype = NPCAIProfileArchetype.Humanoid;
                break;

            case "Monk":
                def.AIBehavior = NPCAIBehavior.AggressiveMelee;
                if (def.AIProfileArchetype == NPCAIProfileArchetype.None
                    || def.AIProfileArchetype == NPCAIProfileArchetype.Animal)
                    def.AIProfileArchetype = NPCAIProfileArchetype.Humanoid;
                break;
        }
    }

    /// <summary>
    /// Generate a unique spawn ID for a creature entry.
    /// Format: "spawn_{baseId}_{class}_{level}_{counter}" or "spawn_{baseId}_{counter}"
    /// </summary>
    private static string GenerateSpawnId(EncounterCreatureEntry entry, int spawnIdx)
    {
        _spawnCounter++;
        string baseId = (entry.BaseCreatureId ?? "unknown").Replace(" ", "_").ToLower();

        if (entry.HasClassLevels)
        {
            string className = entry.TemplateClass.ToLower().Replace(" ", "_");
            return $"spawn_{baseId}_{className}_{entry.TemplateLevel}_{_spawnCounter}";
        }

        return $"spawn_{baseId}_{_spawnCounter}";
    }

    /// <summary>
    /// Convenience method: Prepare a single base creature (no class levels).
    /// </summary>
    public static SpawnResult PrepareCreature(string baseCreatureId, int count = 1)
    {
        var encounter = new EncounterDefinition($"Single: {baseCreatureId}")
            .AddCreature(baseCreatureId, count);
        return PrepareEncounter(encounter);
    }

    /// <summary>
    /// Convenience method: Prepare a single creature with class levels.
    /// </summary>
    public static SpawnResult PrepareCreatureWithClass(string baseCreatureId, string className, int classLevel, int count = 1)
    {
        var encounter = new EncounterDefinition($"{baseCreatureId} {className} {classLevel}")
            .AddCreatureWithClass(baseCreatureId, className, classLevel, count);
        return PrepareEncounter(encounter);
    }

    /// <summary>
    /// Parse an encounter string like "Lizardfolk Druid 5" into its components.
    /// Returns true if parsing succeeded, with the base creature ID, class name,
    /// and class level extracted.
    ///
    /// Parsing strategy:
    ///   1. Try the full string as a creature ID
    ///   2. Try removing the last token as a level number, then the second-to-last as a class name
    ///   3. Progressively shorten from the right to find the longest matching creature ID
    /// </summary>
    public static bool TryParseCreatureString(string input, out string baseCreatureId, out string className, out int classLevel)
    {
        baseCreatureId = null;
        className = null;
        classLevel = 0;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();

        // Normalize to lowercase/underscore ID format
        string idForm = input.Replace(" ", "_").ToLower();

        // Strategy 1: Direct lookup — the whole string is a creature ID
        NPCDatabase.Init();
        if (NPCDatabase.Get(idForm) != null)
        {
            baseCreatureId = idForm;
            return true;
        }

        // Strategy 2: Parse "BaseCreature ClassName Level" format
        string[] tokens = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            // Single word, try as-is
            baseCreatureId = idForm;
            return NPCDatabase.Get(baseCreatureId) != null;
        }

        // Check if last token is a number (level)
        string lastToken = tokens[tokens.Length - 1];
        if (int.TryParse(lastToken, out int parsedLevel) && parsedLevel > 0 && tokens.Length >= 3)
        {
            // Last token is level, second-to-last should be class name
            string possibleClass = tokens[tokens.Length - 2];

            // Try to validate the class
            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(possibleClass);
            if (classDef != null)
            {
                // Everything before the class name is the creature
                string[] creatureTokens = new string[tokens.Length - 2];
                Array.Copy(tokens, creatureTokens, tokens.Length - 2);
                string creatureId = string.Join("_", creatureTokens).ToLower();

                if (NPCDatabase.Get(creatureId) != null)
                {
                    baseCreatureId = creatureId;
                    className = classDef.ClassName; // Use canonical name from registry
                    classLevel = parsedLevel;
                    return true;
                }
            }
        }

        // Strategy 3: Last token is a number but only 2 tokens (e.g., "Warrior 1")
        if (int.TryParse(lastToken, out int parsedLevel2) && parsedLevel2 > 0 && tokens.Length == 2)
        {
            // "ClassName Level" — no separate base creature, check if first token is both a creature and class
            string possibleCreatureAndClass = tokens[0].ToLower();

            // Check if there's a matching creature
            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(tokens[0]);
            if (classDef != null)
            {
                // This is a pure class NPC (e.g., "Warrior 1", "Fighter 3")
                // The base creature would be a generic humanoid — check for race-specific entries
                string raceCreatureId = possibleCreatureAndClass + "_warrior";
                if (NPCDatabase.Get(raceCreatureId) == null)
                    raceCreatureId = possibleCreatureAndClass;

                if (NPCDatabase.Get(raceCreatureId) != null)
                {
                    baseCreatureId = raceCreatureId;
                    className = classDef.ClassName;
                    classLevel = parsedLevel2;
                    return true;
                }
            }
        }

        // Strategy 4: Progressive shortening from the right — find longest matching creature
        for (int take = tokens.Length; take >= 1; take--)
        {
            string[] creatureTokens = new string[take];
            Array.Copy(tokens, creatureTokens, take);
            string candidateId = string.Join("_", creatureTokens).ToLower();

            if (NPCDatabase.Get(candidateId) != null)
            {
                baseCreatureId = candidateId;

                // Check remaining tokens for class + level
                if (take < tokens.Length)
                {
                    string[] remaining = new string[tokens.Length - take];
                    Array.Copy(tokens, take, remaining, 0, remaining.Length);

                    if (remaining.Length >= 2)
                    {
                        string possibleClass = remaining[0];
                        if (int.TryParse(remaining[remaining.Length - 1], out int lvl) && lvl > 0)
                        {
                            ClassRegistry.Init();
                            ICharacterClass cls = ClassRegistry.GetClass(possibleClass);
                            if (cls != null)
                            {
                                className = cls.ClassName;
                                classLevel = lvl;
                            }
                        }
                    }
                    else if (remaining.Length == 1)
                    {
                        // Single remaining token — could be a class with implicit level 1
                        ClassRegistry.Init();
                        ICharacterClass cls = ClassRegistry.GetClass(remaining[0]);
                        if (cls != null)
                        {
                            className = cls.ClassName;
                            classLevel = 1;
                        }
                    }
                }

                return true;
            }
        }

        // Last resort: treat as base creature ID (will fail at spawn time if not found)
        baseCreatureId = idForm;
        return false;
    }

    /// <summary>
    /// Parse an encounter string and create an EncounterCreatureEntry from it.
    /// Returns null if parsing fails completely.
    /// </summary>
    public static EncounterCreatureEntry ParseCreatureEntry(string input, int count = 1)
    {
        if (!TryParseCreatureString(input, out string baseId, out string className, out int level))
        {
            // Even if parsing "failed", baseId will be set to the normalized form
            if (string.IsNullOrEmpty(baseId))
                return null;
        }

        var entry = new EncounterCreatureEntry
        {
            BaseCreatureId = baseId,
            TemplateClass = className,
            TemplateLevel = level,
            Count = count
        };

        return entry;
    }

    /// <summary>
    /// Build an EncounterDefinition from a list of creature description strings.
    /// Each string is parsed to extract base creature + optional class/level.
    ///
    /// Example input:
    ///   ["Lizardfolk Druid 5", "Lizardfolk", "Lizardfolk", "Lizardfolk"]
    ///
    /// Automatically groups identical entries with Count.
    /// </summary>
    public static EncounterDefinition BuildFromStrings(string encounterName, List<string> creatureStrings)
    {
        var encounter = new EncounterDefinition(encounterName);

        if (creatureStrings == null || creatureStrings.Count == 0)
            return encounter;

        // Group identical strings for count aggregation
        var grouped = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < creatureStrings.Count; i++)
        {
            string s = creatureStrings[i];
            if (string.IsNullOrWhiteSpace(s)) continue;

            string key = s.Trim();
            if (grouped.ContainsKey(key))
                grouped[key]++;
            else
                grouped[key] = 1;
        }

        foreach (var kvp in grouped)
        {
            EncounterCreatureEntry entry = ParseCreatureEntry(kvp.Key, kvp.Value);
            if (entry != null)
                encounter.Entries.Add(entry);
            else
                Debug.LogWarning($"[DungeonEncounterSpawner] Could not parse creature string: '{kvp.Key}'");
        }

        return encounter;
    }

    /// <summary>
    /// Clean up temporary spawn registrations from a previous encounter.
    /// Call this before preparing a new encounter to avoid ID pollution.
    /// </summary>
    public static void CleanupSpawnEntries(SpawnResult previousResult)
    {
        if (previousResult == null || previousResult.EnemyIds == null)
            return;

        for (int i = 0; i < previousResult.EnemyIds.Count; i++)
        {
            string id = previousResult.EnemyIds[i];
            if (!string.IsNullOrEmpty(id) && id.StartsWith("spawn_"))
                NPCDatabase.Unregister(id);
        }

        Debug.Log($"[DungeonEncounterSpawner] Cleaned up {previousResult.EnemyIds.Count} temporary spawn entries.");
    }

    /// <summary>Reset the spawn counter (e.g., between sessions).</summary>
    public static void ResetCounter()
    {
        _spawnCounter = 0;
    }
}
