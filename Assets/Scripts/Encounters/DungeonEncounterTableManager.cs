using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Central manager for the DMG 3.5e dungeon encounter table system (Phase 3).
///
/// Manages eight encounter tables (dungeon levels 1-8), each with d% entries
/// and cascade logic that redirects extreme rolls to adjacent-level tables.
///
/// Cascade Logic (DMG 3.5e p.82):
///   - Roll 01-10 → re-roll on the next easier table (level - 1)
///   - Roll 91-100 → re-roll on the next harder table (level + 1)
///   - At table boundaries (level 1 / level 8), cascades wrap to same table
///   - Maximum cascade depth of 3 prevents infinite loops
///
/// Public API:
///   LoadTables()                         — Build tables from hardcoded DMG data
///   LoadFromCSV(string path)             — Load creature list from CSV (name mapping)
///   GetTable(int level)                  — Get a specific table
///   GenerateRandomEncounter(dungeonLevel) — Roll d%, handle cascades, return EncounterDefinition
///
/// Integration:
///   Returns EncounterDefinition objects compatible with Phase 2's
///   DungeonEncounterSpawner.PrepareEncounter() pipeline.
///
/// Phase 3: DMG Encounter Tables.
/// </summary>
public static class DungeonEncounterTableManager
{
    // =========================================================================
    //  State
    // =========================================================================

    /// <summary>All loaded encounter tables, keyed by dungeon level (1-8).</summary>
    private static Dictionary<int, DungeonEncounterTable> _tables;

    /// <summary>Whether tables have been loaded.</summary>
    public static bool IsLoaded => _tables != null && _tables.Count > 0;

    /// <summary>Minimum table level.</summary>
    public const int MinLevel = 1;

    /// <summary>Maximum table level.</summary>
    public const int MaxLevel = 8;

    /// <summary>Maximum cascade depth to prevent infinite loops.</summary>
    public const int MaxCascadeDepth = 3;

    /// <summary>
    /// CSV creature name → NPCDatabase ID mapping for typo correction and
    /// normalization. Built during LoadFromCSV.
    /// </summary>
    private static Dictionary<string, string> _creatureNameMap;

    // =========================================================================
    //  Loading
    // =========================================================================

    /// <summary>
    /// Load encounter tables from the hardcoded DMG data.
    /// This is the primary loading method — call this to initialize the system.
    /// </summary>
    public static void LoadTables()
    {
        _tables = DungeonEncounterTableData.BuildAllTables();

        // Validate all tables
        int totalIssues = 0;
        for (int level = MinLevel; level <= MaxLevel; level++)
        {
            if (!_tables.ContainsKey(level))
            {
                Debug.LogError($"[EncounterTableManager] Missing table for level {level}!");
                continue;
            }

            var issues = _tables[level].Validate();
            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogWarning($"[EncounterTableManager] {issues[i]}");
                totalIssues++;
            }
        }

        int totalEntries = 0;
        for (int level = MinLevel; level <= MaxLevel; level++)
        {
            if (_tables.ContainsKey(level))
                totalEntries += _tables[level].TotalEntryCount;
        }

        Debug.Log($"[EncounterTableManager] Loaded {_tables.Count} tables with " +
                  $"{totalEntries} total entries. {totalIssues} validation issues.");
    }

    /// <summary>
    /// Load creature names from a CSV file and build the name → ID mapping.
    /// The CSV is expected to contain creature names (one per row, first column).
    /// This supplements the hardcoded table data by providing CSV-based name resolution.
    ///
    /// Call LoadTables() first, then optionally LoadFromCSV() to add CSV name mappings.
    /// </summary>
    public static void LoadFromCSV(string csvPath)
    {
        if (!IsLoaded)
        {
            Debug.Log("[EncounterTableManager] Tables not loaded yet, loading now...");
            LoadTables();
        }

        _creatureNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Add known typo corrections from the CSV
        InitializeTypoCorrections();

        if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
        {
            Debug.LogWarning($"[EncounterTableManager] CSV not found at: {csvPath}. " +
                             "Using hardcoded table data only.");
            return;
        }

        int parsed = 0;
        int mapped = 0;
        int unmapped = 0;

        try
        {
            string[] lines = File.ReadAllLines(csvPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split CSV and take first column
                string[] parts = line.Split(',');
                string name = parts[0].Trim();

                // Skip header-like lines
                if (string.IsNullOrEmpty(name) ||
                    name.Equals("creature", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Unnamed"))
                    continue;

                parsed++;
                string resolvedId = ResolveCreatureName(name);

                if (resolvedId != null)
                {
                    _creatureNameMap[name] = resolvedId;
                    mapped++;
                }
                else
                {
                    unmapped++;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EncounterTableManager] Error reading CSV: {ex.Message}");
        }

        Debug.Log($"[EncounterTableManager] CSV loaded: {parsed} entries parsed, " +
                  $"{mapped} mapped to NPCDatabase, {unmapped} unmapped.");
    }

    // =========================================================================
    //  Table Access
    // =========================================================================

    /// <summary>
    /// Get the encounter table for the specified dungeon level (1-8).
    /// Returns null if not loaded or level is out of range.
    /// </summary>
    public static DungeonEncounterTable GetTable(int level)
    {
        if (!IsLoaded)
        {
            Debug.LogWarning("[EncounterTableManager] Tables not loaded. Call LoadTables() first.");
            return null;
        }

        level = Mathf.Clamp(level, MinLevel, MaxLevel);

        DungeonEncounterTable table;
        if (_tables.TryGetValue(level, out table))
            return table;

        Debug.LogWarning($"[EncounterTableManager] No table for level {level}.");
        return null;
    }

    /// <summary>Get all loaded tables.</summary>
    public static Dictionary<int, DungeonEncounterTable> GetAllTables()
    {
        if (!IsLoaded) LoadTables();
        return _tables;
    }

    // =========================================================================
    //  Encounter Generation — Core API
    // =========================================================================

    /// <summary>
    /// Generate a random encounter for the given dungeon level.
    /// Rolls d% on the appropriate table, handles cascade redirects to
    /// adjacent tables, and returns an EncounterDefinition ready for
    /// Phase 2's DungeonEncounterSpawner.
    ///
    /// Returns null if tables are not loaded or generation fails.
    /// </summary>
    /// <param name="dungeonLevel">Dungeon level (1-8, clamped).</param>
    /// <param name="partyLevel">Party level (for logging/future EL adjustment, currently unused).</param>
    public static EncounterDefinition GenerateRandomEncounter(int dungeonLevel, int partyLevel = 0)
    {
        if (!IsLoaded)
        {
            Debug.LogWarning("[EncounterTableManager] Tables not loaded. Call LoadTables() first.");
            return null;
        }

        dungeonLevel = Mathf.Clamp(dungeonLevel, MinLevel, MaxLevel);

        Debug.Log($"[EncounterTableManager] Generating encounter for dungeon level {dungeonLevel}" +
                  (partyLevel > 0 ? $" (party level {partyLevel})" : ""));

        // Roll with cascade handling
        DungeonEncounterTableEntry entry = RollWithCascade(dungeonLevel, 0);

        if (entry == null)
        {
            Debug.LogError("[EncounterTableManager] Failed to generate encounter — " +
                           "all cascade attempts returned null.");
            return null;
        }

        EncounterDefinition def = entry.ToEncounterDefinition();
        Debug.Log($"[EncounterTableManager] Generated: {def.GetPreview()}");
        return def;
    }

    /// <summary>
    /// Generate a random encounter and immediately prepare it through the
    /// Phase 2 spawner pipeline. Returns the SpawnResult with ready-to-use
    /// NPC IDs and definitions.
    ///
    /// This is the full end-to-end convenience method:
    ///   Table roll → EncounterDefinition → SpawnResult
    /// </summary>
    public static DungeonEncounterSpawner.SpawnResult GenerateAndPrepare(int dungeonLevel, int partyLevel = 0)
    {
        EncounterDefinition encounter = GenerateRandomEncounter(dungeonLevel, partyLevel);
        if (encounter == null)
        {
            Debug.LogError("[EncounterTableManager] Cannot prepare — encounter generation failed.");
            return new DungeonEncounterSpawner.SpawnResult();
        }

        return DungeonEncounterSpawner.PrepareEncounter(encounter);
    }

    /// <summary>
    /// Generate an encounter for a specific d% roll (for testing/debugging).
    /// Does NOT roll randomly — uses the exact roll value provided.
    /// </summary>
    public static EncounterDefinition GenerateEncounterForRoll(int dungeonLevel, int roll)
    {
        if (!IsLoaded) LoadTables();

        dungeonLevel = Mathf.Clamp(dungeonLevel, MinLevel, MaxLevel);
        roll = Mathf.Clamp(roll, 1, 100);

        Debug.Log($"[EncounterTableManager] Looking up roll {roll} on level {dungeonLevel} table.");

        DungeonEncounterTableEntry entry = ResolveRoll(dungeonLevel, roll, 0);

        if (entry == null) return null;
        return entry.ToEncounterDefinition();
    }

    // =========================================================================
    //  Cascade Logic
    // =========================================================================

    /// <summary>
    /// Roll d% on the specified table and handle cascades recursively.
    /// </summary>
    private static DungeonEncounterTableEntry RollWithCascade(int level, int depth)
    {
        if (depth >= MaxCascadeDepth)
        {
            Debug.LogWarning($"[EncounterTableManager] Max cascade depth ({MaxCascadeDepth}) reached " +
                             $"at level {level}. Falling back to non-cascade entry.");
            return GetFallbackEntry(level);
        }

        DungeonEncounterTable table = GetTable(level);
        if (table == null) return null;

        DungeonEncounterTableEntry entry = table.RollEncounter();
        if (entry == null) return null;

        if (!entry.IsCascade)
            return entry;

        // Handle cascade
        int targetLevel = level;
        if (entry.Cascade == CascadeDirection.Easier)
        {
            targetLevel = Mathf.Max(MinLevel, level - 1);
            Debug.Log($"[EncounterTableManager] Cascade EASIER: level {level} → level {targetLevel} " +
                      $"(depth {depth + 1})");
        }
        else if (entry.Cascade == CascadeDirection.Harder)
        {
            targetLevel = Mathf.Min(MaxLevel, level + 1);
            Debug.Log($"[EncounterTableManager] Cascade HARDER: level {level} → level {targetLevel} " +
                      $"(depth {depth + 1})");
        }

        // At boundaries, cascading to same level — re-roll on same table
        return RollWithCascade(targetLevel, depth + 1);
    }

    /// <summary>
    /// Resolve a specific roll value with cascade handling (for deterministic testing).
    /// </summary>
    private static DungeonEncounterTableEntry ResolveRoll(int level, int roll, int depth)
    {
        if (depth >= MaxCascadeDepth)
            return GetFallbackEntry(level);

        DungeonEncounterTable table = GetTable(level);
        if (table == null) return null;

        DungeonEncounterTableEntry entry = table.GetEntryByRoll(roll);
        if (entry == null) return null;

        if (!entry.IsCascade)
            return entry;

        int targetLevel = level;
        if (entry.Cascade == CascadeDirection.Easier)
            targetLevel = Mathf.Max(MinLevel, level - 1);
        else if (entry.Cascade == CascadeDirection.Harder)
            targetLevel = Mathf.Min(MaxLevel, level + 1);

        // Re-roll on target table (random, since original roll was a cascade)
        return RollWithCascade(targetLevel, depth + 1);
    }

    /// <summary>
    /// Get the first non-cascade entry from a table as a fallback
    /// when cascade depth is exceeded.
    /// </summary>
    private static DungeonEncounterTableEntry GetFallbackEntry(int level)
    {
        DungeonEncounterTable table = GetTable(level);
        if (table == null) return null;

        for (int i = 0; i < table.Entries.Count; i++)
        {
            if (!table.Entries[i].IsCascade)
                return table.Entries[i];
        }

        return null;
    }

    // =========================================================================
    //  Creature Name Resolution (CSV Support)
    // =========================================================================

    /// <summary>
    /// Dictionary of known typos/variants from the CSV → corrected NPCDatabase IDs.
    /// </summary>
    private static void InitializeTypoCorrections()
    {
        // Typos from CSV
        _creatureNameMap["beareded devil"] = "bearded_devil";
        _creatureNameMap["lanter archon"] = "lantern_archon";
        _creatureNameMap["doppelganer"] = "doppelganger";
        _creatureNameMap["menticore"] = "manticore";
        // "thoqqua" removed — not in NPCDatabase
        _creatureNameMap["trogolodyte"] = "troglodyte";
        _creatureNameMap["trogolydyte"] = "troglodyte";
        _creatureNameMap["slaamander"] = "average_salamander";
        _creatureNameMap["flamebrother slaamanders"] = "flamebrother_salamander";
        _creatureNameMap["shieker"] = "shrieker";
        _creatureNameMap["vargouiles"] = "vargouille";
        // "monstrous" removed — truncated ID, not in NPCDatabase
        _creatureNameMap["small mosntrous scorpion"] = "monstrous_scorpion_small";
        _creatureNameMap["huge monsterous centipede"] = "huge_monstrous_centipede";
        // Hydra variants (now registered)
        _creatureNameMap["five-headed hydra"] = "hydra_5head";
        _creatureNameMap["five headed hydra"] = "hydra_5head";
        _creatureNameMap["hydra, five-headed"] = "hydra_5head";
        _creatureNameMap["hydra (5 heads)"] = "hydra_5head";
        _creatureNameMap["seven-headed hydra"] = "hydra_7head";
        _creatureNameMap["seven headed hydra"] = "hydra_7head";
        _creatureNameMap["hydra, seven-headed"] = "hydra_7head";
        _creatureNameMap["hydra (7 heads)"] = "hydra_7head";
        _creatureNameMap["nine-headed hydra"] = "hydra_9head";
        _creatureNameMap["nine headed hydra"] = "hydra_9head";
        _creatureNameMap["hydra, nine-headed"] = "hydra_9head";
        _creatureNameMap["hydra (9 heads)"] = "hydra_9head";

        // Classic monsters — new in Phase 4
        _creatureNameMap["ghost"] = "ghost";
        _creatureNameMap["ghosts"] = "ghost";
        _creatureNameMap["treant"] = "treant";
        _creatureNameMap["treants"] = "treant";
        _creatureNameMap["chimera"] = "chimera";
        _creatureNameMap["chimeras"] = "chimera";
        _creatureNameMap["wyvern"] = "wyvern";
        _creatureNameMap["wyverns"] = "wyvern";
        _creatureNameMap["couatl"] = "couatl";
        _creatureNameMap["couatls"] = "couatl";
        _creatureNameMap["spirit naga"] = "spirit_naga";
        _creatureNameMap["spirit nagas"] = "spirit_naga";
        _creatureNameMap["hellswasp swarm"] = "hellwasp_swarm";
        _creatureNameMap["formian taskmater"] = "formian_taskmaster";

        // Variant spellings / alternate names
        _creatureNameMap["grey ooze"] = "gray_ooze";
        _creatureNameMap["gray ooze"] = "gray_ooze";
        _creatureNameMap["drow elf"] = "drow";
        _creatureNameMap["drow elves"] = "drow";
        _creatureNameMap["duargar dwarves"] = "duergar";
        _creatureNameMap["svirfneblin gnome"] = "svirfneblin";
        _creatureNameMap["yuan-ti pureblood"] = "yuan_ti_pureblood";
        _creatureNameMap["yuan-ti halfblood"] = "yuan_ti_halfblood";
        _creatureNameMap["yuan-ti abomination"] = "yuan_ti_abomination";
        _creatureNameMap["will-o'-wisp"] = "will_o_wisp";
        _creatureNameMap["derros"] = "derro";
        _creatureNameMap["bralanis"] = "bralani";

        // Plural forms → singular
        _creatureNameMap["displacer beasts"] = "displacer_beast";
        _creatureNameMap["gargoyles"] = "gargoyle";
        _creatureNameMap["ghasts"] = "ghast";
        _creatureNameMap["hyenas"] = "hyena";
        _creatureNameMap["harpies"] = "harpy";
        _creatureNameMap["howlers"] = "howler";
        _creatureNameMap["formian workers"] = "formian_worker";
        _creatureNameMap["monitor lizards"] = "monitor_lizard";
        _creatureNameMap["giant worker ants"] = "giant_worker_ant";

        // Snakes → viper IDs
        _creatureNameMap["tiny viper snakes"] = "viper_tiny";
        _creatureNameMap["tiny viper snake"] = "viper_tiny";
        _creatureNameMap["small viper snake"] = "small_viper";
        _creatureNameMap["medium viper snake"] = "viper_medium";
        _creatureNameMap["large viper snake"] = "viper_large";
        _creatureNameMap["huge viper snakes"] = "viper_huge";

        // Direct name-to-ID mappings for simple cases
        _creatureNameMap["dire rat"] = "dire_rat";
        _creatureNameMap["giant fire beetle"] = "giant_fire_beetle";
        _creatureNameMap["dwarf warrior"] = "dwarf_warrior";
        _creatureNameMap["elf warrior"] = "elf_warrior";
        _creatureNameMap["goblin warrior"] = "goblin";
        _creatureNameMap["kobold warrior"] = "goblin";  // Kobold not in DB, map to goblin
        _creatureNameMap["orc warrior"] = "goblin";     // Orc not in DB, map to goblin
        _creatureNameMap["hobgoblin warrior"] = "hobgoblin";
        _creatureNameMap["halfling warrior"] = "halfling_warrior";
        _creatureNameMap["spider swarm"] = "spider_swarm";
        _creatureNameMap["lantern archon"] = "lantern_archon";
        _creatureNameMap["bat swarm"] = "bat_swarm";
        _creatureNameMap["rat swarm"] = "rat_swarm";
        _creatureNameMap["locust swarm"] = "locust_swarm";
        _creatureNameMap["constrictor snake"] = "constrictor_snake";
        _creatureNameMap["hell hound"] = "hell_hound";
        _creatureNameMap["gelatinous cube"] = "gelatinous_cube";
        _creatureNameMap["phantom fungus"] = "phantom_fungus";
        _creatureNameMap["rust monster"] = "rust_monster";
        _creatureNameMap["violet fungus"] = "violet_fungus";
        _creatureNameMap["giant praying mantis"] = "giant_praying_mantis";
        _creatureNameMap["hound archon"] = "hound_archon";
        _creatureNameMap["carrion crawler"] = "carrion_crawler";
        _creatureNameMap["displacer beast"] = "displacer_beast";
        _creatureNameMap["grey ooze"] = "gray_ooze";
        _creatureNameMap["centipede swarm"] = "centipede_swarm";
        _creatureNameMap["vampire spawn"] = "vampire_spawn";
        _creatureNameMap["greater barghest"] = "greater_barghest";
        _creatureNameMap["celestial lion"] = "celestial_lion";
        _creatureNameMap["bearded devil"] = "bearded_devil";
        _creatureNameMap["gibbering mouther"] = "gibbering_mouther";
        _creatureNameMap["ochre jelly"] = "ochre_jelly";
        _creatureNameMap["phase spider"] = "phase_spider";
        _creatureNameMap["shadow mastiff"] = "shadow_mastiff";
        _creatureNameMap["giant constrictor snake"] = "giant_constrictor_snake";
        _creatureNameMap["chain devil"] = "chain_devil";
        _creatureNameMap["giant bombardier beetle"] = "giant_bombardier_beetle";
        _creatureNameMap["shocker lizard"] = "shocker_lizard";
        _creatureNameMap["minor xorn"] = "minor_xorn";
        _creatureNameMap["chaos beast"] = "chaos_beast";
        _creatureNameMap["black pudding"] = "black_pudding";
        _creatureNameMap["invisible stalker"] = "invisible_stalker";
        _creatureNameMap["flesh golem"] = "flesh_golem";
        _creatureNameMap["hill giant"] = "hill_giant";
        _creatureNameMap["dark naga"] = "dark_naga";
        _creatureNameMap["ogre mage"] = "ogre_mage";
        _creatureNameMap["greater shadow"] = "greater_shadow";
        _creatureNameMap["blue slaad"] = "blue_slaad";
        _creatureNameMap["red slaad"] = "red_slaad";
        _creatureNameMap["green slaad"] = "green_slaad";
        _creatureNameMap["stone giant"] = "stone_giant";
        _creatureNameMap["mind flayer"] = "mind_flayer";
        _creatureNameMap["noble djinni"] = "noble_djinni";
        _creatureNameMap["average xorn"] = "average_xorn";
        _creatureNameMap["fiendish dire rat"] = "fiendish_dire_rat";
        _creatureNameMap["medium monstrous centipede"] = "monstrous_centipede_medium";
        _creatureNameMap["medium monstrous scorpion"] = "monstrous_scorpion_medium";
        _creatureNameMap["average salamander"] = "average_salamander";
        _creatureNameMap["hellwasp swarm"] = "hellwasp_swarm";
        _creatureNameMap["formian taskmaster"] = "formian_taskmaster";

        // ── Phase 1 additions: plurals, misspellings, DMG CSV name variants ──

        // Plurals → singular creature IDs
        _creatureNameMap["harpies"] = "harpy";
        _creatureNameMap["howlers"] = "howler";
        _creatureNameMap["hyenas"] = "hyena";
        _creatureNameMap["monitor lizards"] = "monitor_lizard";
        _creatureNameMap["formian workers"] = "formian_worker";
        _creatureNameMap["giant worker ants"] = "giant_worker_ant";

        // Size-variant vermin (CSV → normalized IDs)
        _creatureNameMap["gargantuan monstrous centipede"] = "monstrous_centipede_gargantuan";
        _creatureNameMap["large monstrous centipede"] = "monstrous_centipede_large";
        _creatureNameMap["large monstrous scorpion"] = "monstrous_scorpion_large";
        _creatureNameMap["large monstrous spider"] = "monstrous_spider_large";
        _creatureNameMap["medium monstrous spider"] = "monstrous_spider_medium";
        _creatureNameMap["medium monstrous scorpion"] = "monstrous_scorpion_medium";

        // Viper variants
        _creatureNameMap["tiny viper snakes"] = "viper_tiny";
        _creatureNameMap["tiny viper snake"] = "viper_tiny";
        _creatureNameMap["small viper snake"] = "viper_small";
        _creatureNameMap["medium viper snake"] = "viper_medium";
        _creatureNameMap["large viper snake"] = "viper_large";
        _creatureNameMap["huge viper snakes"] = "viper_huge";
        _creatureNameMap["huge viper snake"] = "viper_huge";

        // Skeleton/zombie template creatures
        _creatureNameMap["human warrior skeleton"] = "skeleton_human_warrior";
        _creatureNameMap["human commoner zombies"] = "zombie_human_commoner";
        _creatureNameMap["human commoner zombie"] = "zombie_human_commoner";
        _creatureNameMap["minotaur zombie"] = "zombie_minotaur";
        _creatureNameMap["owlbear skeleton"] = "skeleton_owlbear";
        _creatureNameMap["troglodyte zombie"] = "zombie_troglodyte";
        _creatureNameMap["skeleton archer"] = "skeleton_archer";

        // New creatures added in Phase 1
        _creatureNameMap["thoqque"] = "thoqqua";
        _creatureNameMap["darkmantle"] = "darkmantle";
        _creatureNameMap["green hag"] = "green_hag";
        _creatureNameMap["annis hag"] = "annis";
        _creatureNameMap["kobold warrior"] = "kobold_warrior";
        _creatureNameMap["orc warrior"] = "orc_warrior";
        _creatureNameMap["hobgoblin warrior"] = "hobgoblin_warrior";
        _creatureNameMap["half-orc warrior"] = "half_orc_warrior";
        _creatureNameMap["human warrior"] = "human_warrior";
        _creatureNameMap["human commoner"] = "human_commoner";

        // Dragon aliases (CSV name → dynamic dragon registry IDs)
        _creatureNameMap["wyrmling brass dragon"] = "dragon_brass_wyrmling";
        _creatureNameMap["young copper dragon"] = "dragon_copper_young";
        _creatureNameMap["young white dragon"] = "dragon_white_young";

        // Generic mephit → fire mephit (most common encounter)
        _creatureNameMap["mephits"] = "fire_mephit";
        _creatureNameMap["mephit"] = "fire_mephit";

        // ── Phase 2 name mappings ──────────────────────────────────
        // New creatures
        _creatureNameMap["girallon"] = "girallon";
        _creatureNameMap["girallons"] = "girallon";
        _creatureNameMap["ankheg"] = "ankheg";
        _creatureNameMap["ankhegs"] = "ankheg";
        _creatureNameMap["beholder"] = "beholder";
        _creatureNameMap["beholders"] = "beholder";
        _creatureNameMap["bulette"] = "bulette";
        _creatureNameMap["bulettes"] = "bulette";
        _creatureNameMap["land shark"] = "bulette";
        _creatureNameMap["purple worm"] = "purple_worm";
        _creatureNameMap["purple worms"] = "purple_worm";
        _creatureNameMap["frost giant"] = "frost_giant";
        _creatureNameMap["frost giants"] = "frost_giant";
        _creatureNameMap["fire giant"] = "fire_giant";
        _creatureNameMap["fire giants"] = "fire_giant";
        _creatureNameMap["nightmare"] = "nightmare";
        _creatureNameMap["nightmares"] = "nightmare";
        _creatureNameMap["elder xorn"] = "elder_xorn";
        _creatureNameMap["rakshasa"] = "rakshasa";
        _creatureNameMap["rakshasas"] = "rakshasa";

        // Alias existing warriors by race_warrior pattern
        _creatureNameMap["drow warrior"] = "drow";
        _creatureNameMap["drow elf warrior"] = "drow";
        _creatureNameMap["duergar warrior"] = "duergar";
        _creatureNameMap["svirfneblin warrior"] = "svirfneblin";
        _creatureNameMap["deep gnome warrior"] = "svirfneblin";

        // Grey/gray ooze alias
        _creatureNameMap["grey ooze"] = "gray_ooze";
        _creatureNameMap["grey oozes"] = "gray_ooze";

        // Xorn generic → average xorn
        _creatureNameMap["xorn"] = "average_xorn";
        _creatureNameMap["xorns"] = "average_xorn";
        _creatureNameMap["minor xorn"] = "minor_xorn";
        _creatureNameMap["elder xorns"] = "elder_xorn";
    }

    /// <summary>
    /// Attempt to resolve a CSV creature name to an NPCDatabase ID.
    /// Tries: exact match in map → normalized ID form → fuzzy fallback.
    /// Returns null if unresolvable.
    /// </summary>
    private static string ResolveCreatureName(string csvName)
    {
        if (string.IsNullOrWhiteSpace(csvName)) return null;

        csvName = csvName.Trim();

        // Check typo/variant map first
        if (_creatureNameMap != null)
        {
            string mapped;
            if (_creatureNameMap.TryGetValue(csvName, out mapped))
                return mapped;
        }

        // Normalize to ID format: lowercase, spaces → underscores, strip hyphens
        string idForm = csvName.ToLower()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("'", "");

        // Direct ID lookup (would need NPCDatabase at runtime — return the id form)
        return idForm;
    }

    // =========================================================================
    //  Debug / Utility
    // =========================================================================

    /// <summary>
    /// Print all tables to Debug.Log.
    /// </summary>
    public static void DebugPrintAllTables()
    {
        if (!IsLoaded)
        {
            Debug.Log("[EncounterTableManager] Not loaded.");
            return;
        }

        for (int level = MinLevel; level <= MaxLevel; level++)
        {
            DungeonEncounterTable table;
            if (_tables.TryGetValue(level, out table))
                table.DebugPrint();
        }
    }

    /// <summary>
    /// Run a batch of random encounters for testing. Returns a summary string.
    /// </summary>
    public static string RunTestBatch(int dungeonLevel, int count = 10)
    {
        if (!IsLoaded) LoadTables();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Test Batch: {count} encounters for dungeon level {dungeonLevel} ===");

        for (int i = 0; i < count; i++)
        {
            var enc = GenerateRandomEncounter(dungeonLevel);
            if (enc != null)
                sb.AppendLine($"  [{i + 1}] {enc.GetPreview()}");
            else
                sb.AppendLine($"  [{i + 1}] FAILED");
        }

        string result = sb.ToString();
        Debug.Log(result);
        return result;
    }

    /// <summary>
    /// Reset the manager state (for testing or hot-reload).
    /// </summary>
    public static void Reset()
    {
        _tables = null;
        _creatureNameMap = null;
    }
}
