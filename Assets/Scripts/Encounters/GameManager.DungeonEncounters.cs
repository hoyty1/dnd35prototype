using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager partial class: Dungeon Encounter Integration (Phase 2 + Phase 4 UI).
///
/// Bridges DungeonEncounterSpawner with the existing combat flow.
/// Provides methods to start combat from EncounterDefinitions,
/// creature strings, or pre-parsed SpawnResults.
///
/// Phase 4 additions:
///   - DungeonEncounterGeneratorUI integration via OpenDungeonEncounterGenerator()
///   - Automatic table loading and party level detection
///   - Full pipeline: UI → Table Roll → EncounterDefinition → SpawnResult → Combat
///
/// Usage from UI/scripts:
///   GameManager.Instance.StartDungeonEncounter(encounterDef);
///   GameManager.Instance.StartDungeonEncounterFromStrings("Raid", new List&lt;string&gt; {
///       "Lizardfolk Druid 5", "Lizardfolk", "Lizardfolk", "Lizardfolk"
///   });
///   GameManager.Instance.OpenDungeonEncounterGenerator();  // Phase 4 UI
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The most recently prepared dungeon encounter result.
    /// Kept for cleanup between encounters.
    /// </summary>
    private DungeonEncounterSpawner.SpawnResult _lastDungeonEncounterResult;

    /// <summary>
    /// Reference to the DMG dungeon encounter generator UI (Phase 4).
    /// Created on demand via OpenDungeonEncounterGenerator().
    /// </summary>
    private DungeonEncounterGeneratorUI _dungeonEncounterGeneratorUI;

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
            CombatUI.ShowCombatLog(CombatLogHelper.Buff("⚔", $"Dungeon encounter: {encounter.Name ?? "unnamed"} ({result.Count} creatures)"));
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
            CombatUI.ShowCombatLog(CombatLogHelper.Buff("⚔", $"Dungeon encounter: {name} ({result.Count} creatures)"));
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

    // =========================================================================
    //  Phase 4: DMG Encounter Table UI Integration
    // =========================================================================

    /// <summary>
    /// Open the DMG dungeon encounter generator UI (Phase 4).
    /// Shows a full-screen panel with dungeon level selection, d% rolling,
    /// encounter preview, and combat start integration.
    ///
    /// The UI handles:
    ///   1. Level selection (1-8)
    ///   2. d% rolling with cascade logic via DungeonEncounterTableManager
    ///   3. Encounter preview display
    ///   4. "Start Combat" → calls StartDungeonEncounter() → combat pipeline
    ///
    /// Call from PreCombatHubUI, EncounterSelectionUI, or any menu context.
    /// </summary>
    /// <param name="onBack">Optional callback when user clicks "Back" (e.g., return to encounter selection).</param>
    /// <param name="defaultDungeonLevel">Override the default dungeon level (0 = auto-detect from party level).</param>
    public void OpenDungeonEncounterGenerator(System.Action onBack = null, int defaultDungeonLevel = 0)
    {
        // Ensure encounter tables are loaded
        if (!DungeonEncounterTableManager.IsLoaded)
        {
            Debug.Log("[GameManager.DungeonEncounters] Loading DMG encounter tables...");
            DungeonEncounterTableManager.LoadTables();
        }

        // Create UI component on demand
        EnsureDungeonEncounterGeneratorUI();

        if (_dungeonEncounterGeneratorUI == null)
        {
            Debug.LogError("[GameManager.DungeonEncounters] Failed to create DungeonEncounterGeneratorUI.");
            return;
        }

        // Close other overlays that might conflict
        PreCombatHubUI?.Close();

        int partyLevel = GetCurrentPartyAverageLevel();
        Debug.Log($"[GameManager.DungeonEncounters] Opening DMG encounter generator | partyLevel={partyLevel} | defaultLevel={defaultDungeonLevel}");

        _dungeonEncounterGeneratorUI.Open(
            partyLevel: partyLevel,
            onStartCombat: (encounter) =>
            {
                Debug.Log($"[GameManager.DungeonEncounters] DMG encounter selected for combat: {encounter?.Name ?? "null"}");
                StartDungeonEncounter(encounter);
                OpenPreCombatHubPhase();
            },
            onBack: () =>
            {
                Debug.Log("[GameManager.DungeonEncounters] DMG encounter generator closed via Back.");
                onBack?.Invoke();
            },
            defaultDungeonLevel: defaultDungeonLevel);
    }

    /// <summary>
    /// Close the DMG encounter generator UI if it's open.
    /// </summary>
    public void CloseDungeonEncounterGenerator()
    {
        if (_dungeonEncounterGeneratorUI != null && _dungeonEncounterGeneratorUI.IsOpen)
            _dungeonEncounterGeneratorUI.Close();
    }

    /// <summary>
    /// Whether the DMG encounter generator UI is currently visible.
    /// </summary>
    public bool IsDungeonEncounterGeneratorOpen =>
        _dungeonEncounterGeneratorUI != null && _dungeonEncounterGeneratorUI.IsOpen;

    /// <summary>
    /// Create or find the DungeonEncounterGeneratorUI component.
    /// </summary>
    private void EnsureDungeonEncounterGeneratorUI()
    {
        if (_dungeonEncounterGeneratorUI != null) return;

        _dungeonEncounterGeneratorUI = FindObjectOfType<DungeonEncounterGeneratorUI>();
        if (_dungeonEncounterGeneratorUI == null)
            _dungeonEncounterGeneratorUI = gameObject.AddComponent<DungeonEncounterGeneratorUI>();
    }
}
