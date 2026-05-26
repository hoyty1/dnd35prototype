using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager partial class: Dungeon Encounter Integration (Phase 2).
///
/// Bridges DungeonEncounterSpawner with the existing combat flow.
/// Provides methods to start combat from EncounterDefinitions,
/// creature strings, or pre-parsed SpawnResults.
///
/// Usage from UI/scripts:
///   GameManager.Instance.StartDungeonEncounter(encounterDef);
///   GameManager.Instance.StartDungeonEncounterFromStrings("Raid", new List&lt;string&gt; {
///       "Lizardfolk Druid 5", "Lizardfolk", "Lizardfolk", "Lizardfolk"
///   });
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The most recently prepared dungeon encounter result.
    /// Kept for cleanup between encounters.
    /// </summary>
    private DungeonEncounterSpawner.SpawnResult _lastDungeonEncounterResult;

    /// <summary>
    /// Start a dungeon encounter from a fully defined EncounterDefinition.
    /// Prepares all creatures (base + class levels + templates) and feeds
    /// the resulting NPC IDs into the standard combat pipeline.
    /// </summary>
    public void StartDungeonEncounter(EncounterDefinition encounter)
    {
        if (encounter == null)
        {
            Debug.LogError("[GameManager.DungeonEncounters] Cannot start null encounter.");
            return;
        }

        // Clean up any previous dynamic spawn entries
        if (_lastDungeonEncounterResult != null)
        {
            DungeonEncounterSpawner.CleanupSpawnEntries(_lastDungeonEncounterResult);
            _lastDungeonEncounterResult = null;
        }

        // Prepare all creatures
        DungeonEncounterSpawner.SpawnResult result = DungeonEncounterSpawner.PrepareEncounter(encounter);
        _lastDungeonEncounterResult = result;

        if (!result.IsValid)
        {
            Debug.LogError($"[GameManager.DungeonEncounters] Encounter preparation failed: {encounter.Name}");
            if (result.Warnings.Count > 0)
            {
                for (int i = 0; i < result.Warnings.Count; i++)
                    Debug.LogError($"  Warning: {result.Warnings[i]}");
            }
            return;
        }

        Debug.Log($"[GameManager.DungeonEncounters] Starting encounter '{encounter.Name}' " +
                  $"with {result.Count} creatures.");

        // Feed into the existing encounter pipeline via ApplyRandomEncounter
        // (which sets _activeEncounterEnemyIds, calls SetupEnemyEncounter, etc.)
        ApplyRandomEncounter(result.EnemyIds, null);

        if (CombatUI != null)
            CombatUI.ShowCombatLog($"⚔ Dungeon encounter: {encounter.Name ?? "unnamed"} ({result.Count} creatures)");
    }

    /// <summary>
    /// Start a dungeon encounter from a list of creature description strings.
    /// Parses each string to extract base creature + optional class/level.
    ///
    /// Example:
    ///   StartDungeonEncounterFromStrings("Lizardfolk Raid", new List&lt;string&gt; {
    ///       "Lizardfolk Druid 5",
    ///       "Lizardfolk",
    ///       "Lizardfolk",
    ///       "Lizardfolk"
    ///   });
    /// </summary>
    public void StartDungeonEncounterFromStrings(string name, List<string> creatureStrings)
    {
        EncounterDefinition encounter = DungeonEncounterSpawner.BuildFromStrings(name, creatureStrings);
        StartDungeonEncounter(encounter);
    }

    /// <summary>
    /// Start a dungeon encounter from a pre-prepared SpawnResult.
    /// Use this when you need to inspect/modify the result before starting combat.
    /// </summary>
    public void StartDungeonEncounterFromResult(DungeonEncounterSpawner.SpawnResult result)
    {
        if (result == null || !result.IsValid)
        {
            Debug.LogError("[GameManager.DungeonEncounters] Cannot start encounter from invalid SpawnResult.");
            return;
        }

        // Clean up previous
        if (_lastDungeonEncounterResult != null && _lastDungeonEncounterResult != result)
            DungeonEncounterSpawner.CleanupSpawnEntries(_lastDungeonEncounterResult);

        _lastDungeonEncounterResult = result;

        Debug.Log($"[GameManager.DungeonEncounters] Starting encounter from SpawnResult " +
                  $"with {result.Count} creatures.");

        ApplyRandomEncounter(result.EnemyIds, null);

        if (CombatUI != null)
        {
            string name = result.Source != null ? result.Source.Name : "unnamed";
            CombatUI.ShowCombatLog($"⚔ Dungeon encounter: {name} ({result.Count} creatures)");
        }
    }

    /// <summary>
    /// Prepare a dungeon encounter without starting combat.
    /// Useful for preview/inspection before committing.
    /// Returns the SpawnResult for examination.
    /// </summary>
    public DungeonEncounterSpawner.SpawnResult PrepareDungeonEncounter(EncounterDefinition encounter)
    {
        if (encounter == null) return null;

        // Clean up previous
        if (_lastDungeonEncounterResult != null)
        {
            DungeonEncounterSpawner.CleanupSpawnEntries(_lastDungeonEncounterResult);
            _lastDungeonEncounterResult = null;
        }

        DungeonEncounterSpawner.SpawnResult result = DungeonEncounterSpawner.PrepareEncounter(encounter);
        _lastDungeonEncounterResult = result;
        return result;
    }
}
