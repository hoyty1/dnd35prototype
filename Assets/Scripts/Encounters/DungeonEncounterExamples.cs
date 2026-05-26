using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Example dungeon encounters demonstrating the Phase 2 dynamic spawn system.
/// Contains sample encounter definitions from the dungeon encounters CSV
/// and utility methods for creating common encounter patterns.
///
/// These examples serve both as documentation and as ready-to-use presets
/// that Phase 3 (encounter tables) can reference.
/// </summary>
public static class DungeonEncounterExamples
{
    // ═══════════════════════════════════════════════════════════════════
    //  EXAMPLE 1: Base creatures only (no class levels)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simple encounter: 4 base Lizardfolk (CR 1 each).
    /// No class levels or templates applied.
    /// </summary>
    public static EncounterDefinition LizardfolkPatrol()
    {
        return new EncounterDefinition("Lizardfolk Patrol")
            .AddCreature("lizardfolk", count: 4);
    }

    /// <summary>
    /// Boss encounter: single Ettin (CR 6).
    /// </summary>
    public static EncounterDefinition EttinAmbush()
    {
        return new EncounterDefinition("Ettin Ambush")
            .AddCreature("ettin", count: 1);
    }

    /// <summary>
    /// Swarm encounter: 3 Ogres (CR 3 each).
    /// </summary>
    public static EncounterDefinition OgreWarband()
    {
        return new EncounterDefinition("Ogre Warband")
            .AddCreature("ogre", count: 3);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EXAMPLE 2: Creatures with class levels
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mixed encounter: 1 Lizardfolk Druid 5 (leader) + 3 base Lizardfolk (minions).
    /// The Druid gets class levels applied dynamically at spawn time.
    /// </summary>
    public static EncounterDefinition LizardfolkRaidingParty()
    {
        return new EncounterDefinition("Lizardfolk Raiding Party")
            .AddCreatureWithClass("lizardfolk", "Druid", 5, count: 1)
            .AddCreature("lizardfolk", count: 3);
    }

    /// <summary>
    /// Elite encounter: 2 Hobgoblin Fighters 3 + 1 Hobgoblin Cleric 3.
    /// </summary>
    public static EncounterDefinition HobgoblinEliteSquad()
    {
        return new EncounterDefinition("Hobgoblin Elite Squad")
            .AddCreatureWithClass("hobgoblin", "Fighter", 3, count: 2)
            .AddCreatureWithClass("hobgoblin", "Cleric", 3, count: 1);
    }

    /// <summary>
    /// Boss encounter: 1 Ogre Barbarian 3 (CR 6+).
    /// </summary>
    public static EncounterDefinition OgreBarbarian()
    {
        return new EncounterDefinition("Ogre Barbarian")
            .AddCreatureWithClass("ogre", "Barbarian", 3, count: 1);
    }

    /// <summary>
    /// Mixed encounter: Dwarf Warriors with a Fighter leader.
    /// From CSV: "Dwarf Warrior 1" entries with a "Dwarf Fighter 4" leader.
    /// </summary>
    public static EncounterDefinition DwarfWarband()
    {
        return new EncounterDefinition("Dwarf Warband")
            .AddCreatureWithClass("dwarf_warrior", "Fighter", 4, count: 1)
            .AddCreatureWithClass("dwarf_warrior", "Warrior", 1, count: 4);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EXAMPLE 3: Creatures with creature templates
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Templated encounter: 2 Celestial Lions.
    /// Base creature "lion" with "celestial" template applied at spawn time.
    /// </summary>
    public static EncounterDefinition CelestialLionPair()
    {
        return new EncounterDefinition("Celestial Lion Guardians")
            .AddTemplatedCreature("lion", "celestial", count: 2);
    }

    /// <summary>
    /// Templated encounter: 1 Fiendish Dire Rat pack.
    /// </summary>
    public static EncounterDefinition FiendishDireRatPack()
    {
        return new EncounterDefinition("Fiendish Dire Rat Pack")
            .AddTemplatedCreature("dire_rat", "fiendish", count: 4);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EXAMPLE 4: Complex mixed encounters (from the CSV)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Underground encounter from CSV: Mind Flayer with Grimlock thralls.
    /// Mind Flayer (CR 8) + 4 Grimlocks (CR 1 each).
    /// </summary>
    public static EncounterDefinition MindFlayerLair()
    {
        return new EncounterDefinition("Mind Flayer Lair")
            .AddCreature("mind_flayer", count: 1)
            .AddCreature("grimlock", count: 4);
    }

    /// <summary>
    /// Undead encounter: Mummy with Ghoul minions.
    /// </summary>
    public static EncounterDefinition UndeadCrypt()
    {
        return new EncounterDefinition("Undead Crypt")
            .AddCreature("mummy", count: 1)
            .AddCreature("ghast", count: 2)
            .AddCreature("ghoul", count: 3);
    }

    /// <summary>
    /// Outsider encounter: Erinyes with Bearded Devil guards.
    /// </summary>
    public static EncounterDefinition DevilishAmbush()
    {
        return new EncounterDefinition("Devilish Ambush")
            .AddCreature("erinyes", count: 1)
            .AddCreature("bearded_devil", count: 2);
    }

    /// <summary>
    /// Aberration encounter: Aboleth with Skum minions.
    /// </summary>
    public static EncounterDefinition AbolethPool()
    {
        return new EncounterDefinition("Aboleth's Pool")
            .AddCreature("aboleth", count: 1)
            .AddCreature("skum", count: 4);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EXAMPLE 5: String parsing (CSV-style input)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Demonstrate building an encounter from CSV-style strings.
    /// This is the primary input format for Phase 3 (encounter tables).
    /// </summary>
    public static EncounterDefinition FromCSVStrings()
    {
        var creatureStrings = new List<string>
        {
            "Lizardfolk Druid 5",
            "Lizardfolk",
            "Lizardfolk",
            "Lizardfolk"
        };
        return DungeonEncounterSpawner.BuildFromStrings("CSV Parsed Encounter", creatureStrings);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UTILITY: Prepare and log an example encounter
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Run all examples through the spawner to validate they work.
    /// Logs results to Unity console. Does not start combat.
    /// </summary>
    public static void ValidateAllExamples()
    {
        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log("  DungeonEncounterExamples — Validation Run");
        Debug.Log("═══════════════════════════════════════════════════════");

        ValidateEncounter("Lizardfolk Patrol", LizardfolkPatrol());
        ValidateEncounter("Ettin Ambush", EttinAmbush());
        ValidateEncounter("Ogre Warband", OgreWarband());
        ValidateEncounter("Lizardfolk Raiding Party", LizardfolkRaidingParty());
        ValidateEncounter("Hobgoblin Elite Squad", HobgoblinEliteSquad());
        ValidateEncounter("Ogre Barbarian", OgreBarbarian());
        ValidateEncounter("Dwarf Warband", DwarfWarband());
        ValidateEncounter("Celestial Lion Pair", CelestialLionPair());
        ValidateEncounter("Fiendish Dire Rat Pack", FiendishDireRatPack());
        ValidateEncounter("Mind Flayer Lair", MindFlayerLair());
        ValidateEncounter("Undead Crypt", UndeadCrypt());
        ValidateEncounter("Devilish Ambush", DevilishAmbush());
        ValidateEncounter("Aboleth Pool", AbolethPool());
        ValidateEncounter("CSV Parsed", FromCSVStrings());

        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log("  Validation complete.");
        Debug.Log("═══════════════════════════════════════════════════════");
    }

    private static void ValidateEncounter(string label, EncounterDefinition encounter)
    {
        if (encounter == null)
        {
            Debug.LogError($"[Validate] {label}: FAILED — null encounter");
            return;
        }

        DungeonEncounterSpawner.SpawnResult result = DungeonEncounterSpawner.PrepareEncounter(encounter);

        if (result.IsValid)
        {
            Debug.Log($"[Validate] ✓ {label}: {result.Count} creatures prepared");
            for (int i = 0; i < result.Definitions.Count; i++)
            {
                NPCDefinition def = result.Definitions[i];
                Debug.Log($"    {def.Name} — HD {def.HitDice}, HP {def.BaseHitDieHP}, " +
                          $"CR {def.ChallengeRating}, BAB +{def.BAB}");
            }
        }
        else
        {
            Debug.LogError($"[Validate] ✗ {label}: FAILED with {result.Warnings.Count} warnings");
            for (int i = 0; i < result.Warnings.Count; i++)
                Debug.LogError($"    - {result.Warnings[i]}");
        }

        // Clean up
        DungeonEncounterSpawner.CleanupSpawnEntries(result);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UTILITY: Get all example encounters as a list (for UI integration)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns all example encounters. Useful for a dropdown or selection UI.
    /// </summary>
    public static List<EncounterDefinition> GetAllExamples()
    {
        return new List<EncounterDefinition>
        {
            LizardfolkPatrol(),
            EttinAmbush(),
            OgreWarband(),
            LizardfolkRaidingParty(),
            HobgoblinEliteSquad(),
            OgreBarbarian(),
            DwarfWarband(),
            CelestialLionPair(),
            FiendishDireRatPack(),
            MindFlayerLair(),
            UndeadCrypt(),
            DevilishAmbush(),
            AbolethPool()
        };
    }
}
