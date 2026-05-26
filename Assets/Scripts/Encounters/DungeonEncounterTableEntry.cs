using System;
using System.Collections.Generic;

/// <summary>
/// Direction a cascade roll should redirect to when a d% result falls
/// in the top or bottom 10% of a dungeon encounter table.
/// Phase 3: DMG Encounter Tables.
/// </summary>
public enum CascadeDirection
{
    /// <summary>Normal result — use this table's entry.</summary>
    None,
    /// <summary>Roll 01-10: re-roll on the easier (lower-level) table.</summary>
    Easier,
    /// <summary>Roll 91-100: re-roll on the harder (higher-level) table.</summary>
    Harder
}

/// <summary>
/// One row in a dungeon encounter table. Maps a d% range to a set of creatures
/// that form an encounter. Supports conversion to EncounterDefinition for the
/// Phase 2 spawner pipeline.
///
/// Phase 3: DMG Encounter Tables.
/// </summary>
[Serializable]
public class DungeonEncounterTableEntry
{
    /// <summary>Minimum d% roll (inclusive, 1-100).</summary>
    public int MinRoll;

    /// <summary>Maximum d% roll (inclusive, 1-100).</summary>
    public int MaxRoll;

    /// <summary>Encounter Level for this entry.</summary>
    public int EL;

    /// <summary>
    /// If not None, this entry is a cascade redirect — ignore creature data
    /// and re-roll on the indicated adjacent table.
    /// </summary>
    public CascadeDirection Cascade = CascadeDirection.None;

    /// <summary>
    /// Human-readable description of the encounter (e.g., "1d3 Bugbears").
    /// Used for logging and debug display.
    /// </summary>
    public string Description = "";

    /// <summary>
    /// Pre-built creature entries for this encounter row.
    /// Each element describes a creature type + count.
    /// </summary>
    public List<EncounterCreatureEntry> Creatures = new List<EncounterCreatureEntry>();

    /// <summary>Whether the d% roll falls within this entry's range.</summary>
    public bool MatchesRoll(int roll)
    {
        return roll >= MinRoll && roll <= MaxRoll;
    }

    /// <summary>Whether this is a cascade redirect rather than a real encounter.</summary>
    public bool IsCascade => Cascade != CascadeDirection.None;

    /// <summary>
    /// Convert this entry into an EncounterDefinition compatible with
    /// the Phase 2 DungeonEncounterSpawner.
    /// If creature entries have dice expressions (Phase 5 CSV-loaded),
    /// the dice are rolled here to produce concrete integer counts.
    /// </summary>
    public EncounterDefinition ToEncounterDefinition()
    {
        var def = new EncounterDefinition(Description);
        def.TargetEL = EL;
        def.Environment = "Underground";

        for (int i = 0; i < Creatures.Count; i++)
        {
            // Clone the entry so the table data stays immutable
            var src = Creatures[i];
            var clone = new EncounterCreatureEntry
            {
                BaseCreatureId = src.BaseCreatureId,
                TemplateClass = src.TemplateClass,
                TemplateLevel = src.TemplateLevel,
                Count = src.Count,
                CountExpression = src.CountExpression, // Phase 5: carry over dice expression
                CreatureTemplateIds = src.CreatureTemplateIds != null
                    ? new List<string>(src.CreatureTemplateIds) : null
            };

            // Phase 5: resolve dice expression to concrete count
            clone.ResolveCount();

            def.Entries.Add(clone);
        }

        return def;
    }

    /// <summary>
    /// Convenience builder: create a basic entry for one creature type.
    /// </summary>
    public static DungeonEncounterTableEntry Basic(int minRoll, int maxRoll, int el,
        string creatureId, int count = 1, string description = null)
    {
        var entry = new DungeonEncounterTableEntry
        {
            MinRoll = minRoll,
            MaxRoll = maxRoll,
            EL = el,
            Description = description ?? $"{count}x {creatureId}"
        };
        entry.Creatures.Add(new EncounterCreatureEntry(creatureId, count));
        return entry;
    }

    /// <summary>
    /// Convenience builder: create a cascade redirect entry.
    /// </summary>
    public static DungeonEncounterTableEntry CascadeEntry(int minRoll, int maxRoll, CascadeDirection dir)
    {
        return new DungeonEncounterTableEntry
        {
            MinRoll = minRoll,
            MaxRoll = maxRoll,
            EL = 0,
            Cascade = dir,
            Description = dir == CascadeDirection.Easier
                ? "Cascade → easier table" : "Cascade → harder table"
        };
    }

    /// <summary>
    /// Convenience builder: entry with a classed creature.
    /// </summary>
    public static DungeonEncounterTableEntry Classed(int minRoll, int maxRoll, int el,
        string creatureId, string className, int classLevel, int count = 1, string description = null)
    {
        var entry = new DungeonEncounterTableEntry
        {
            MinRoll = minRoll,
            MaxRoll = maxRoll,
            EL = el,
            Description = description ?? $"{count}x {creatureId} {className} {classLevel}"
        };
        entry.Creatures.Add(new EncounterCreatureEntry(creatureId, className, classLevel, count));
        return entry;
    }

    /// <summary>
    /// Convenience builder: entry with a templated creature (e.g., skeleton, celestial).
    /// </summary>
    public static DungeonEncounterTableEntry Templated(int minRoll, int maxRoll, int el,
        string creatureId, string templateId, int count = 1, string description = null)
    {
        var entry = new DungeonEncounterTableEntry
        {
            MinRoll = minRoll,
            MaxRoll = maxRoll,
            EL = el,
            Description = description ?? $"{count}x {templateId} {creatureId}"
        };
        var creature = new EncounterCreatureEntry(creatureId, count);
        creature.CreatureTemplateIds = new List<string> { templateId };
        entry.Creatures.Add(creature);
        return entry;
    }

    public override string ToString()
    {
        if (IsCascade)
            return $"[{MinRoll:D2}-{MaxRoll:D2}] {Description}";
        return $"[{MinRoll:D2}-{MaxRoll:D2}] EL {EL}: {Description}";
    }
}
