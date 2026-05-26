using System;
using System.Collections.Generic;

/// <summary>
/// Describes a single creature entry within a dungeon encounter.
/// Supports base creatures, base + class levels (e.g., "Lizardfolk Druid 5"),
/// and optional creature templates (celestial, fiendish, etc.).
///
/// Phase 2: Dynamic encounter spawn system.
/// </summary>
[Serializable]
public class EncounterCreatureEntry
{
    /// <summary>
    /// NPCDatabase ID of the base creature (e.g., "lizardfolk", "ogre", "dwarf_warrior").
    /// </summary>
    public string BaseCreatureId;

    /// <summary>
    /// Optional character class to apply (e.g., "Druid", "Fighter", "Barbarian").
    /// Must match a class name registered in ClassRegistry.
    /// Null/empty means no class levels are applied.
    /// </summary>
    public string TemplateClass;

    /// <summary>
    /// Number of class levels to apply. Only used when TemplateClass is set.
    /// Must be >= 1 if TemplateClass is specified.
    /// </summary>
    public int TemplateLevel;

    /// <summary>
    /// Optional creature templates to apply (e.g., "celestial", "fiendish", "skeleton").
    /// These are applied via CreatureTemplateRegistry at spawn time.
    /// </summary>
    public List<string> CreatureTemplateIds;

    /// <summary>
    /// Number of this creature to spawn. Defaults to 1.
    /// For entries loaded from CSV with dice expressions, this value is set
    /// when <see cref="ResolveCount"/> is called (typically during
    /// <see cref="DungeonEncounterTableEntry.ToEncounterDefinition"/>).
    /// </summary>
    public int Count = 1;

    /// <summary>
    /// Optional dice expression for variable creature counts (e.g., "1d3+1").
    /// When set, <see cref="ResolveCount"/> rolls the dice and updates
    /// <see cref="Count"/>. Null for entries with fixed counts.
    /// Phase 5: Dice-based encounter counts.
    /// </summary>
    public DiceExpression CountExpression;

    /// <summary>
    /// Roll the dice expression (if any) and set <see cref="Count"/> to the result.
    /// If no dice expression is set, <see cref="Count"/> remains unchanged.
    /// Called during encounter generation to produce a concrete spawn count.
    /// </summary>
    /// <returns>The resolved count value.</returns>
    public int ResolveCount()
    {
        if (CountExpression != null && !CountExpression.IsFixed)
        {
            Count = CountExpression.Roll();
        }
        else if (CountExpression != null && CountExpression.IsFixed)
        {
            Count = CountExpression.Modifier;
        }
        return Count;
    }

    /// <summary>Whether this entry specifies class levels.</summary>
    public bool HasClassLevels => !string.IsNullOrEmpty(TemplateClass) && TemplateLevel > 0;

    /// <summary>Whether this entry specifies creature templates.</summary>
    public bool HasCreatureTemplates => CreatureTemplateIds != null && CreatureTemplateIds.Count > 0;

    /// <summary>
    /// Display name for logging/UI (e.g., "Lizardfolk Druid 5", "Ogre", "Celestial Lion").
    /// </summary>
    public string DisplayName
    {
        get
        {
            string name = BaseCreatureId ?? "unknown";
            if (HasClassLevels)
                name += $" {TemplateClass} {TemplateLevel}";
            if (HasCreatureTemplates)
                name = string.Join(" ", CreatureTemplateIds) + " " + name;
            if (Count > 1)
                name = $"{Count}x {name}";
            return name;
        }
    }

    public EncounterCreatureEntry() { }

    public EncounterCreatureEntry(string baseCreatureId, int count = 1)
    {
        BaseCreatureId = baseCreatureId;
        Count = count;
    }

    public EncounterCreatureEntry(string baseCreatureId, string templateClass, int templateLevel, int count = 1)
    {
        BaseCreatureId = baseCreatureId;
        TemplateClass = templateClass;
        TemplateLevel = templateLevel;
        Count = count;
    }
}

/// <summary>
/// Describes a complete dungeon encounter: one or more creature entries
/// that should be spawned together as an encounter group.
///
/// Examples:
///   - 3 Hobgoblin Fighters 3 + 1 Hobgoblin Cleric 5
///   - 1 Ogre Barbarian 3
///   - 4 Lizardfolk (base, no class)
///   - 1 Ettin (single boss)
///
/// Used by DungeonEncounterSpawner to generate the NPC list for combat.
/// </summary>
[Serializable]
public class EncounterDefinition
{
    /// <summary>Optional name for logging and UI display.</summary>
    public string Name;

    /// <summary>
    /// The creature entries that make up this encounter.
    /// Each entry can be a different creature type with different class levels.
    /// </summary>
    public List<EncounterCreatureEntry> Entries = new List<EncounterCreatureEntry>();

    /// <summary>Target Encounter Level for validation/display.</summary>
    public int TargetEL;

    /// <summary>Optional environment tag for theming (e.g., "Underground", "Forest").</summary>
    public string Environment;

    /// <summary>Total number of creatures across all entries.</summary>
    public int TotalCreatureCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Entries.Count; i++)
                total += Math.Max(1, Entries[i].Count);
            return total;
        }
    }

    public EncounterDefinition() { }

    public EncounterDefinition(string name)
    {
        Name = name;
    }

    /// <summary>Add a base creature with no class levels.</summary>
    public EncounterDefinition AddCreature(string baseCreatureId, int count = 1)
    {
        Entries.Add(new EncounterCreatureEntry(baseCreatureId, count));
        return this;
    }

    /// <summary>Add a creature with class levels (e.g., "lizardfolk" + "Druid" + 5).</summary>
    public EncounterDefinition AddCreatureWithClass(string baseCreatureId, string className, int classLevel, int count = 1)
    {
        Entries.Add(new EncounterCreatureEntry(baseCreatureId, className, classLevel, count));
        return this;
    }

    /// <summary>Add a creature with a creature template (e.g., celestial lion).</summary>
    public EncounterDefinition AddTemplatedCreature(string baseCreatureId, string creatureTemplateId, int count = 1)
    {
        var entry = new EncounterCreatureEntry(baseCreatureId, count);
        entry.CreatureTemplateIds = new List<string> { creatureTemplateId };
        Entries.Add(entry);
        return this;
    }

    /// <summary>
    /// Build a preview string showing all creatures in the encounter.
    /// </summary>
    public string GetPreview()
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(Name))
            sb.AppendLine(Name);
        for (int i = 0; i < Entries.Count; i++)
            sb.AppendLine($"  - {Entries[i].DisplayName}");
        if (TargetEL > 0)
            sb.AppendLine($"  Target EL: {TargetEL}");
        return sb.ToString().TrimEnd();
    }
}
