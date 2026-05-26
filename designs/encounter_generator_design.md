# Random Encounter Generator — System Design Document

> **Project**: D&D 3.5e Prototype (`dnd35prototype`)  
> **Phase**: Phase 5 — Random Encounter Generator  
> **Author**: AI Assistant  
> **Date**: 2026-05-26  
> **Status**: DESIGN ONLY — No implementation code  
> **Branch**: `master`

---

## Table of Contents

1. [Overview](#1-overview)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Existing Architecture Summary](#3-existing-architecture-summary)
4. [Gap Analysis](#4-gap-analysis)
5. [Data Source: CSV Format](#5-data-source-csv-format)
6. [New System Architecture](#6-new-system-architecture)
7. [Data Structures](#7-data-structures)
8. [Algorithms](#8-algorithms)
9. [Integration Plan](#9-integration-plan)
10. [Code Examples](#10-code-examples)
11. [Error Handling & Fallbacks](#11-error-handling--fallbacks)
12. [Testing Strategy](#12-testing-strategy)

---

## 1. Overview

This document describes the design for a **random dungeon encounter generator** that faithfully implements the DMG 3.5e encounter tables (Dungeon Levels 1–9). The system parses encounter descriptions from a CSV data source, supports dice-expression-based creature counts (e.g., `1d3+1`), compound multi-creature entries (e.g., `1 ettercap and 1d3+1 Medium monstrous spiders`), and NPC class-leveled entries (e.g., `5th-level human monk NPC`).

The generator integrates with the existing Phase 2/3 encounter infrastructure, producing `EncounterDefinition` objects that feed directly into `DungeonEncounterSpawner.PrepareEncounter()`.

### Key Distinction from Phase 3

Phase 3 (current) uses **hardcoded tables** in `DungeonEncounterTableData.cs` with **fixed integer counts**. Phase 5 introduces:

- **Dice expression parsing** — counts resolved at roll time, not build time
- **CSV-driven table loading** — tables built from `dungeon_encounters.csv` instead of hand-coded C#
- **Compound entries** — single d% row spawns multiple creature types
- **Table 9** — extends the system from 8 tables to 9
- **Annotation handling** — parenthetical tags like `(vermin)`, `(lycanthrope)`, `(pyro- or cryo-)`

---

## 2. Goals & Non-Goals

### Goals

| # | Goal |
|---|------|
| G1 | Parse all 267 encounter rows from `dungeon_encounters.csv` into runtime table entries |
| G2 | Support dice notation (`1d3`, `2d4+1`, `1d4+4`) with roll-time resolution |
| G3 | Handle compound entries with `" and "` conjunction (multiple creature groups per row) |
| G4 | Handle NPC entries like `5th-level human monk NPC` via existing Classed entry pattern |
| G5 | Extend table range from 1–8 to 1–9 |
| G6 | Maintain full backward compatibility — existing `GenerateRandomEncounter()` API unchanged |
| G7 | Produce `EncounterDefinition` objects compatible with `DungeonEncounterSpawner` pipeline |
| G8 | Strip and optionally log parenthetical annotations without breaking creature ID resolution |
| G9 | Graceful fallback when a creature ID cannot be resolved in NPCDatabase |

### Non-Goals

| # | Non-Goal |
|---|----------|
| N1 | Implementing new creature stat blocks (handled by separate creature addition phases) |
| N2 | Changing the spawner or combat system |
| N3 | Building a visual encounter table editor |
| N4 | CR/EL calculation from scratch — EL values will be looked up or estimated |
| N5 | Implementing creature templates (celestial, fiendish) at the template-engine level |

---

## 3. Existing Architecture Summary

### Current Pipeline (Phase 2/3)

```
GameManager.DungeonEncounters
    └─→ DungeonEncounterTableManager.GenerateRandomEncounter(level)
            ├─→ LoadTables()  →  DungeonEncounterTableData.BuildAllTables()
            │       └─→ Returns Dictionary<int, DungeonEncounterTable>  (levels 1-8)
            ├─→ RollWithCascade(level, depth)
            │       ├─→ Roll d% (1-100)
            │       ├─→ Match entry via DungeonEncounterTableEntry.MatchesRoll(roll)
            │       ├─→ If cascade: recurse on adjacent table (max depth 3)
            │       └─→ Return DungeonEncounterTableEntry
            └─→ entry.ToEncounterDefinition()
                    └─→ EncounterDefinition (cloned creature entries)
                            └─→ DungeonEncounterSpawner.PrepareEncounter()
                                    └─→ SpawnResult (NPCDatabase lookups, positioning)
```

### Key Classes

| Class | Role | File |
|-------|------|------|
| `DungeonEncounterTableEntry` | One d% row: roll range, EL, creatures, cascade | `DungeonEncounterTableEntry.cs` |
| `DungeonEncounterTable` | Container for a level's entries, validation | `DungeonEncounterTable.cs` |
| `DungeonEncounterTableData` | Hardcoded table builder (levels 1-8) | `DungeonEncounterTableData.cs` |
| `DungeonEncounterTableManager` | Public API, cascade logic, name mapping | `DungeonEncounterTableManager.cs` |
| `EncounterCreatureEntry` | Creature ID + count + optional class/template | `EncounterDefinition.cs` |
| `EncounterDefinition` | Complete encounter: list of creature entries | `EncounterDefinition.cs` |
| `DungeonEncounterSpawner` | Spawns NPCs from definition | `DungeonEncounterSpawner.cs` |

### Current Limitations

1. **`EncounterCreatureEntry.Count`** is `int` — no dice expressions
2. **Tables hardcoded** in C# — adding/editing requires recompilation
3. **Only tables 1–8** — table 9 missing
4. **No compound entries** — each row has a single creature type (or manually added multi-creature via `Creatures` list)
5. **No CSV-to-table loader** — `LoadFromCSV()` only builds name mappings, not table entries

---

## 4. Gap Analysis

### Encounter Description Pattern Catalog

Analysis of all 267 CSV rows reveals these distinct patterns:

#### Pattern 1: Simple — `{count} {creature_name}`
```
1 darkmantle
1 ogre
1 basilisk
```
**Frequency**: ~40% of entries  
**Handling**: Direct mapping to `EncounterCreatureEntry(creatureId, count)`

#### Pattern 2: Dice Count — `{dice_expr} {creature_name}`
```
1d3 Medium monstrous centipedes (vermin)
1d4+2 kobold warriors
2d4+1 goblin warriors
1d4+4 dire bats
```
**Frequency**: ~35% of entries  
**Handling**: New `DiceExpression` class, resolved at encounter generation time

#### Pattern 3: Compound — `{group1} and {group2}`
```
1 ettercap and 1d3+1 Medium monstrous spiders (vermin)
1d3 wererats (lycanthrope) and 2d4 dire rats
1 wereboar (lycanthrope) and 1d3 boars
1d3+1 ghasts (ghoul) and 2d4+1 ghouls
1 formian taskmaster and 1 dominated 5th-level human barbarian NPC
```
**Frequency**: ~10% of entries  
**Handling**: Split on `" and "`, parse each group independently

#### Pattern 4: NPC Entry — `{level}-level {race} {class} NPC`
```
5th-level human monk NPC
5th-level kobold sorcerer NPC
5th-level lizardfolk druid NPC (with crocodile)
5th-level hobgoblin fighter NPC and 5th-level goblin rogue NPC
1d3 5th-level troglodyte cleric NPCs
```
**Frequency**: ~5% of entries  
**Handling**: Regex parse → `EncounterCreatureEntry` with `TemplateClass` and `TemplateLevel`

#### Pattern 5: Cascade — `Roll on {N}th-level table`
```
Roll on 2nd-level table
Roll on 1st-level table
```
**Frequency**: ~7% of entries (one per table, top and bottom)  
**Handling**: Already supported via `CascadeDirection`

#### Pattern 6: Annotated — Parenthetical tags
```
(vermin), (animal), (demon), (devil), (lycanthrope), (ghoul), (fungus)
(beholder), (eladrin), (genie), (hag), (ooze), (slaad)
(pyro- or cryo-)
```
**Handling**: Strip from creature name before ID resolution; optionally log for variant selection

#### Pattern 7: Special/Complex
```
"1 ghost, 5th-level fighter" — named creature with class levels
"1 vampire, 5th-level fighter" — named creature with class levels  
"1 ogre barbarian, 4th level" — creature with inline class
"1 half-dragon 4th-level fighter" — template + class
"1d3 devils, hellcat" — comma-separated variant specification
```
**Handling**: Special-case regex patterns; fallback to description-only entry if unparseable

---

## 5. Data Source: CSV Format

### File: `dungeon_encounters.csv`

```
Dungeon_Level,Roll_Min,Roll_Max,Encounter
1,1,3,1d3 Medium monstrous centipedes (vermin)
1,4,8,1d4 dire rats
...
9,43,44,1 formian myrmarch and 2d4+1 formian workers
```

**Columns**:
| Column | Type | Description |
|--------|------|-------------|
| `Dungeon_Level` | int | Table level (1-9) |
| `Roll_Min` | int | Minimum d% roll (inclusive) |
| `Roll_Max` | int | Maximum d% roll (inclusive) |
| `Encounter` | string | Full encounter description (may be quoted if contains commas) |

**Note**: The CSV has 267 data rows across 9 dungeon levels. Level 9's data in the current CSV appears truncated (ends at row 43-44); the remaining entries from the DMG images need to be added separately.

---

## 6. New System Architecture

### Architecture Diagram

```
dungeon_encounters.csv
        │
        ▼
┌─────────────────────────────┐
│  EncounterCSVParser         │  NEW — Parses CSV into raw entry data
│  (static utility class)     │
└──────────┬──────────────────┘
           │ List<RawEncounterRow>
           ▼
┌─────────────────────────────┐
│  EncounterDescriptionParser │  NEW — Parses encounter description strings
│  (static utility class)     │       into structured creature groups
└──────────┬──────────────────┘
           │ ParsedEncounterDescription
           ▼
┌─────────────────────────────┐
│  DiceExpression             │  NEW — Parses and rolls dice notation
│  (value class)              │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  DungeonEncounterTableData  │  MODIFIED — Add BuildFromCSV() alongside
│  (static class)             │  existing BuildAllTables()
└──────────┬──────────────────┘
           │ Dictionary<int, DungeonEncounterTable>
           ▼
┌─────────────────────────────┐
│  DungeonEncounterTableEntry │  EXTENDED — Add DiceCount field
│  (existing class)           │  alongside fixed Count
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  DungeonEncounterTableMgr   │  MODIFIED — MaxLevel 8→9,
│  (existing static class)    │  dice resolution in ToEncounterDefinition
└──────────┬──────────────────┘
           │ EncounterDefinition
           ▼
┌─────────────────────────────┐
│  DungeonEncounterSpawner    │  UNCHANGED — receives EncounterDefinition
│  (existing class)           │  with resolved integer counts
└─────────────────────────────┘
```

### Design Principles

1. **Additive, not destructive** — New classes alongside existing ones; existing hardcoded tables remain as fallback
2. **Dice resolution at the boundary** — `DiceExpression` stored in table entries, resolved to `int` when creating `EncounterDefinition` so spawner sees only concrete counts
3. **Parse once, roll many** — CSV parsed at load time into structured entries; only dice are re-rolled per encounter
4. **Graceful degradation** — Unparseable entries logged and stored as description-only with fallback creature IDs

---

## 7. Data Structures

### 7.1 DiceExpression

A lightweight value type for dice notation parsing and rolling.

```csharp
/// <summary>
/// Represents a dice expression like "1d3", "2d4+1", "1d4+4", or a fixed "3".
/// Immutable after construction. Supports parsing from string and rolling.
/// Phase 5: Random Encounter Generator.
/// </summary>
[Serializable]
public class DiceExpression
{
    /// <summary>Number of dice to roll (0 for fixed values).</summary>
    public readonly int NumDice;

    /// <summary>Number of sides per die (e.g., 3, 4, 6, 8).</summary>
    public readonly int DiceSides;

    /// <summary>Flat modifier added after rolling (can be 0 or negative).</summary>
    public readonly int Modifier;

    /// <summary>Whether this is a fixed value (no dice roll needed).</summary>
    public bool IsFixed => NumDice == 0;

    /// <summary>The minimum possible result.</summary>
    public int Minimum => IsFixed ? Modifier : NumDice + Modifier;

    /// <summary>The maximum possible result.</summary>
    public int Maximum => IsFixed ? Modifier : (NumDice * DiceSides) + Modifier;

    /// <summary>Original string representation.</summary>
    public readonly string Original;
}
```

**Parsing rules**:
- `"3"` → `DiceExpression(numDice=0, sides=0, modifier=3)` — fixed value
- `"1d3"` → `DiceExpression(numDice=1, sides=3, modifier=0)`
- `"2d4+1"` → `DiceExpression(numDice=2, sides=4, modifier=1)`
- `"1d4+4"` → `DiceExpression(numDice=1, sides=4, modifier=4)`

**Regex**: `^(\d+)(?:d(\d+)([+-]\d+)?)?$`

### 7.2 ParsedCreatureGroup

Represents one creature group parsed from an encounter description.

```csharp
/// <summary>
/// One creature group from a parsed encounter description.
/// E.g., "1d3+1 Medium monstrous spiders (vermin)" becomes:
///   CountExpression = DiceExpression("1d3+1")
///   CreatureName = "Medium monstrous spiders"
///   Annotation = "vermin"
///   NpcClass = null
///   NpcLevel = 0
/// Phase 5: Random Encounter Generator.
/// </summary>
public class ParsedCreatureGroup
{
    /// <summary>Dice expression for creature count.</summary>
    public DiceExpression CountExpression;

    /// <summary>Normalized creature name (annotation stripped).</summary>
    public string CreatureName;

    /// <summary>
    /// Parenthetical annotation if present (e.g., "vermin", "lycanthrope", "demon").
    /// Null if no annotation.
    /// </summary>
    public string Annotation;

    /// <summary>NPC class name if this is an NPC entry (e.g., "monk", "fighter").</summary>
    public string NpcClass;

    /// <summary>NPC class level if this is an NPC entry (e.g., 5).</summary>
    public int NpcLevel;

    /// <summary>NPC race if this is an NPC entry (e.g., "human", "kobold").</summary>
    public string NpcRace;

    /// <summary>Whether this group is a class-leveled NPC.</summary>
    public bool IsNpc => !string.IsNullOrEmpty(NpcClass) && NpcLevel > 0;

    /// <summary>Creature template IDs if applicable (e.g., "fiendish", "celestial").</summary>
    public List<string> TemplateIds;
}
```

### 7.3 ParsedEncounterDescription

The full result of parsing one CSV encounter string.

```csharp
/// <summary>
/// Complete parsed result from one encounter description string.
/// May contain one or more creature groups (compound entries).
/// Phase 5: Random Encounter Generator.
/// </summary>
public class ParsedEncounterDescription
{
    /// <summary>Original description string from CSV.</summary>
    public string RawDescription;

    /// <summary>Whether this is a cascade entry ("Roll on Xth-level table").</summary>
    public bool IsCascade;

    /// <summary>Target table level for cascade entries.</summary>
    public int CascadeTargetLevel;

    /// <summary>Parsed creature groups (empty for cascade entries).</summary>
    public List<ParsedCreatureGroup> Groups = new List<ParsedCreatureGroup>();

    /// <summary>Whether parsing encountered issues (logged but entry still usable).</summary>
    public bool HasWarnings;

    /// <summary>Warning messages from parsing.</summary>
    public List<string> Warnings = new List<string>();
}
```

### 7.4 Extended EncounterCreatureEntry

The existing `EncounterCreatureEntry.Count` field is `int`. To support dice expressions without breaking the spawner, we add a **parallel field**:

```csharp
// Added to EncounterCreatureEntry:

/// <summary>
/// Optional dice expression for count. When set, Count is ignored until
/// ResolveCount() is called, which rolls the dice and sets Count.
/// Phase 5: Dice-based encounter counts.
/// </summary>
public DiceExpression CountExpression;

/// <summary>
/// Roll the dice expression (if any) and set Count to the result.
/// If no dice expression, Count remains as-is.
/// Returns the resolved count.
/// </summary>
public int ResolveCount()
{
    if (CountExpression != null && !CountExpression.IsFixed)
    {
        Count = CountExpression.Roll();
    }
    return Count;
}
```

**Key insight**: The `Count` field is always a concrete integer by the time `DungeonEncounterSpawner` sees it. Dice resolution happens in `ToEncounterDefinition()`, maintaining spawner compatibility.

### 7.5 Updated ToEncounterDefinition Flow

```csharp
// Modified in DungeonEncounterTableEntry:
public EncounterDefinition ToEncounterDefinition()
{
    var def = new EncounterDefinition(Description);
    def.TargetEL = EL;
    def.Environment = "Underground";

    for (int i = 0; i < Creatures.Count; i++)
    {
        var src = Creatures[i];
        var clone = new EncounterCreatureEntry
        {
            BaseCreatureId = src.BaseCreatureId,
            TemplateClass = src.TemplateClass,
            TemplateLevel = src.TemplateLevel,
            Count = src.Count,
            CountExpression = src.CountExpression,  // NEW: carry over expression
            CreatureTemplateIds = src.CreatureTemplateIds != null
                ? new List<string>(src.CreatureTemplateIds) : null
        };

        // NEW: Resolve dice expression to concrete count
        clone.ResolveCount();

        def.Entries.Add(clone);
    }

    return def;
}
```

---

## 8. Algorithms

### 8.1 CSV Parsing Pipeline

```
CSV File
  │
  ├─ Read all lines, skip header
  │
  ├─ For each line:
  │   ├─ Split: Dungeon_Level, Roll_Min, Roll_Max, Encounter
  │   ├─ Handle quoted fields (CSV with commas in descriptions)
  │   └─ Yield RawEncounterRow(level, min, max, description)
  │
  └─ Group by Dungeon_Level → Dictionary<int, List<RawEncounterRow>>
```

### 8.2 Description Parsing Algorithm

```
Input: "1 ettercap and 1d3+1 Medium monstrous spiders (vermin)"

Step 1: Check for cascade pattern
  Regex: ^Roll on (\d+)(?:st|nd|rd|th)-level table$
  → Not a cascade, continue

Step 2: Split on " and " to get groups
  → ["1 ettercap", "1d3+1 Medium monstrous spiders (vermin)"]

Step 3: For each group, attempt patterns in priority order:

  Pattern A — NPC entry:
    Regex: ^(?:(\d+(?:d\d+(?:[+-]\d+)?)?)\s+)?(\d+)(?:st|nd|rd|th)-level\s+(\w+)\s+(\w+)\s+NPCs?
    Example: "5th-level human monk NPC"
    → NpcLevel=5, NpcRace="human", NpcClass="monk", Count=1

  Pattern B — Standard creature:
    Regex: ^(\d+(?:d\d+(?:[+-]\d+)?)?)\s+(.+?)(?:\s*\(([^)]+)\))?$
    Example: "1d3+1 Medium monstrous spiders (vermin)"
    → Count="1d3+1", Name="Medium monstrous spiders", Annotation="vermin"

  Pattern C — Special comma format:
    Regex: ^(\d+)\s+(.+?),\s*(\d+)(?:st|nd|rd|th)-level\s+(\w+)$
    Example: "1 ghost, 5th-level fighter"
    → Count=1, Name="ghost", ClassLevel=5, Class="fighter"

Step 4: Parse count expression via DiceExpression.Parse()

Step 5: Resolve creature name → NPCDatabase ID via _creatureNameMap

Output: ParsedEncounterDescription with 2 groups
```

### 8.3 Creature Name Resolution

The existing `_creatureNameMap` (600+ entries) handles most name variations. The new parser should:

1. **Strip annotations**: `"Medium monstrous spiders (vermin)"` → `"Medium monstrous spiders"`
2. **Normalize whitespace and case**: `"  Dire  Rats "` → `"dire rats"`
3. **Strip plurals**: `"ghouls"` → `"ghoul"`, `"kobold warriors"` → `"kobold warrior"`
4. **Look up in `_creatureNameMap`**: `"dire rat"` → `"dire_rat"`
5. **Fallback**: If not found, try underscore-joined lowercase: `"dire rat"` → `"dire_rat"`
6. **Log warning** if still unresolved; entry remains with `BaseCreatureId = raw_name`

```
Resolution Chain:
  raw_name
    → stripAnnotation(raw_name)
    → normalize(stripped)
    → depluralize(normalized)
    → _creatureNameMap.TryGetValue(depluralized)
    → fallback: depluralized.Replace(" ", "_").ToLowerInvariant()
    → log warning if NPCDatabase.GetNPCData(id) returns null
```

### 8.4 Encounter Generation (Updated)

```
GenerateRandomEncounter(int dungeonLevel):
  1. EnsureLoaded()
  2. Clamp dungeonLevel to [MinLevel, MaxLevel]  // now 1-9
  3. entry = RollWithCascade(dungeonLevel, depth=0)
  4. if entry == null → return fallback definition
  5. definition = entry.ToEncounterDefinition()  // dice rolled HERE
  6. return definition
```

The cascade logic is unchanged — only `MaxLevel` increases from 8 to 9.

### 8.5 EL Estimation

The CSV does not include EL values. Options (in priority order):

1. **Lookup table**: Maintain a `Dictionary<string, int>` mapping creature IDs to their CR; calculate EL from CR + count using DMG formula
2. **Hardcoded EL per row**: Add an optional `EL` column to the CSV (or a companion file)
3. **Default EL = dungeon level**: Use the table's dungeon level as a rough EL estimate

**Recommended**: Option 3 for initial implementation (EL = dungeon level), with Option 1 as a Phase 5.1 enhancement. The EL field is used for display purposes only and does not affect spawning.

---

## 9. Integration Plan

### 9.1 Files to Create

| File | Purpose |
|------|---------|
| `Assets/Scripts/Encounters/DiceExpression.cs` | Dice notation parser and roller |
| `Assets/Scripts/Encounters/EncounterDescriptionParser.cs` | Parses encounter description strings |
| `Assets/Scripts/Encounters/EncounterCSVParser.cs` | Reads CSV file into raw data rows |

### 9.2 Files to Modify

| File | Changes |
|------|---------|
| `EncounterCreatureEntry` (in `EncounterDefinition.cs`) | Add `CountExpression` field and `ResolveCount()` method |
| `DungeonEncounterTableEntry.cs` | Update `ToEncounterDefinition()` to call `ResolveCount()` |
| `DungeonEncounterTableData.cs` | Add `BuildFromCSV(string csvPath)` method |
| `DungeonEncounterTableManager.cs` | Change `MaxLevel` to 9; add CSV loading option; update `LoadTables()` |
| `DungeonEncounterGeneratorUI.cs` | Update level slider max from 8 to 9 |
| `GameManager.DungeonEncounters.cs` | Update level range references |

### 9.3 Files NOT Modified

| File | Reason |
|------|--------|
| `DungeonEncounterSpawner.cs` | Receives `EncounterDefinition` with resolved counts — no changes needed |
| `DungeonEncounterTable.cs` | Entry container works as-is for tables 1-9 |
| `NPCDatabase_*.cs` | Creature data additions are a separate phase |

### 9.4 Integration Sequence

```
1. DiceExpression.cs                    — standalone, no dependencies
2. EncounterDescriptionParser.cs        — depends on DiceExpression
3. EncounterCSVParser.cs                — depends on EncounterDescriptionParser
4. Modify EncounterCreatureEntry        — add CountExpression + ResolveCount
5. Modify DungeonEncounterTableEntry    — update ToEncounterDefinition
6. Modify DungeonEncounterTableData     — add BuildFromCSV
7. Modify DungeonEncounterTableManager  — MaxLevel=9, CSV loading
8. Modify UI                            — level slider range
```

### 9.5 Loading Strategy

```csharp
// In DungeonEncounterTableManager.LoadTables():
public static void LoadTables()
{
    // Try CSV first (Phase 5 data)
    string csvPath = Path.Combine(Application.streamingAssetsPath, "dungeon_encounters.csv");
    if (File.Exists(csvPath))
    {
        _tables = DungeonEncounterTableData.BuildFromCSV(csvPath, _creatureNameMap);
        Debug.Log($"[EncounterTableManager] Loaded {_tables.Count} tables from CSV");
    }
    else
    {
        // Fallback to hardcoded Phase 3 data
        _tables = DungeonEncounterTableData.BuildAllTables();
        Debug.Log("[EncounterTableManager] Loaded hardcoded tables (CSV not found)");
    }

    // Validate...
}
```

This ensures:
- **CSV present** → Use dynamic parsing with dice expressions
- **CSV missing** → Fall back to existing hardcoded tables (backward compat)

---

## 10. Code Examples

### 10.1 DiceExpression — Core Methods

```csharp
/// <summary>
/// Parse a string like "1d3+1" or "5" into a DiceExpression.
/// Returns null if the string cannot be parsed.
/// </summary>
public static DiceExpression Parse(string input)
{
    if (string.IsNullOrWhiteSpace(input)) return null;
    input = input.Trim();

    // Try dice pattern: NdS+M or NdS
    var match = Regex.Match(input, @"^(\d+)d(\d+)([+-]\d+)?$");
    if (match.Success)
    {
        int numDice = int.Parse(match.Groups[1].Value);
        int sides = int.Parse(match.Groups[2].Value);
        int mod = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        return new DiceExpression(numDice, sides, mod, input);
    }

    // Try fixed number
    if (int.TryParse(input, out int fixedVal))
    {
        return new DiceExpression(0, 0, fixedVal, input);
    }

    return null; // unparseable
}

/// <summary>
/// Roll the dice and return the result. Fixed values return Modifier directly.
/// </summary>
public int Roll()
{
    if (IsFixed) return Modifier;

    int total = Modifier;
    for (int i = 0; i < NumDice; i++)
    {
        total += UnityEngine.Random.Range(1, DiceSides + 1);
    }
    return Math.Max(1, total); // Ensure at least 1 creature
}
```

### 10.2 EncounterDescriptionParser — Main Entry Point

```csharp
/// <summary>
/// Parse a complete encounter description string from the CSV.
/// Handles cascade entries, compound entries, NPC entries, and annotations.
/// </summary>
public static ParsedEncounterDescription Parse(string description)
{
    var result = new ParsedEncounterDescription { RawDescription = description };

    // Check cascade
    var cascadeMatch = Regex.Match(description, @"^Roll on (\d+)(?:st|nd|rd|th)-level table$");
    if (cascadeMatch.Success)
    {
        result.IsCascade = true;
        result.CascadeTargetLevel = int.Parse(cascadeMatch.Groups[1].Value);
        return result;
    }

    // Split compound entries on " and " (but not within NPC descriptions)
    string[] groups = SplitCompoundEntry(description);

    foreach (string groupStr in groups)
    {
        var group = ParseSingleGroup(groupStr.Trim());
        if (group != null)
            result.Groups.Add(group);
        else
        {
            result.HasWarnings = true;
            result.Warnings.Add($"Could not parse group: '{groupStr}'");
        }
    }

    return result;
}
```

### 10.3 Compound Entry Splitting

```csharp
/// <summary>
/// Split a compound encounter description on " and ", handling edge cases
/// like "hobgoblin warrior and 1d4 goblin warriors" correctly.
/// </summary>
private static string[] SplitCompoundEntry(string description)
{
    // Split on " and " where the right side starts with a digit or
    // an ordinal number (Nth-level), indicating a new creature group
    var parts = new List<string>();
    var regex = new Regex(@"\s+and\s+(?=\d)");
    var splits = regex.Split(description);

    if (splits.Length == 0) return new[] { description };
    return splits;
}
```

### 10.4 NPC Entry Parsing

```csharp
/// <summary>
/// Try to parse an NPC entry like "5th-level human monk NPC".
/// Returns a ParsedCreatureGroup or null if not an NPC pattern.
/// </summary>
private static ParsedCreatureGroup TryParseNpcEntry(string text)
{
    // Pattern: [count] Nth-level {race} {class} NPC[s]
    var match = Regex.Match(text,
        @"^(?:(\d+(?:d\d+(?:[+-]\d+)?)?)\s+)?(\d+)(?:st|nd|rd|th)-level\s+(\w+)\s+(\w+)\s+NPCs?",
        RegexOptions.IgnoreCase);

    if (!match.Success) return null;

    string countStr = match.Groups[1].Success ? match.Groups[1].Value : "1";
    int level = int.Parse(match.Groups[2].Value);
    string race = match.Groups[3].Value.ToLowerInvariant();
    string className = match.Groups[4].Value.ToLowerInvariant();

    return new ParsedCreatureGroup
    {
        CountExpression = DiceExpression.Parse(countStr),
        NpcRace = race,
        NpcClass = className,
        NpcLevel = level,
        CreatureName = $"{race}_{className}_{level}"
    };
}
```

### 10.5 Building a Table Entry from Parsed Data

```csharp
/// <summary>
/// Convert a parsed CSV row into a DungeonEncounterTableEntry.
/// </summary>
public static DungeonEncounterTableEntry BuildEntry(
    int rollMin, int rollMax, int dungeonLevel,
    ParsedEncounterDescription parsed,
    Dictionary<string, string> nameMap)
{
    // Cascade entry
    if (parsed.IsCascade)
    {
        var dir = parsed.CascadeTargetLevel < dungeonLevel
            ? CascadeDirection.Easier : CascadeDirection.Harder;
        return DungeonEncounterTableEntry.CascadeEntry(rollMin, rollMax, dir);
    }

    // Normal encounter entry
    var entry = new DungeonEncounterTableEntry
    {
        MinRoll = rollMin,
        MaxRoll = rollMax,
        EL = dungeonLevel, // Default EL estimate
        Description = parsed.RawDescription
    };

    foreach (var group in parsed.Groups)
    {
        var creature = new EncounterCreatureEntry();

        if (group.IsNpc)
        {
            // NPC with class levels
            string raceId = ResolveCreatureName(group.NpcRace, nameMap);
            creature.BaseCreatureId = raceId;
            creature.TemplateClass = CapitalizeFirst(group.NpcClass);
            creature.TemplateLevel = group.NpcLevel;
        }
        else
        {
            // Standard creature
            creature.BaseCreatureId = ResolveCreatureName(group.CreatureName, nameMap);
        }

        // Set count — dice expression stored for roll-time resolution
        if (group.CountExpression != null)
        {
            creature.CountExpression = group.CountExpression;
            creature.Count = group.CountExpression.Minimum; // Default to minimum
        }
        else
        {
            creature.Count = 1;
        }

        // Templates
        if (group.TemplateIds != null && group.TemplateIds.Count > 0)
            creature.CreatureTemplateIds = new List<string>(group.TemplateIds);

        entry.Creatures.Add(creature);
    }

    return entry;
}
```

---

## 11. Error Handling & Fallbacks

### 11.1 Parse Failures

| Scenario | Handling |
|----------|----------|
| Unparseable dice expression | Log warning, default to count = 1 |
| Unknown creature name | Log warning, use raw name as ID, spawner will skip gracefully |
| Malformed CSV line | Skip line, log error with line number |
| Empty encounter description | Skip entry |
| Compound split produces empty group | Skip that group, log warning |

### 11.2 Runtime Fallbacks

| Scenario | Handling |
|----------|----------|
| CSV file not found | Fall back to hardcoded Phase 3 tables |
| CSV produces empty table for a level | Fall back to hardcoded table for that level |
| Creature not in NPCDatabase | `DungeonEncounterSpawner` already handles missing creatures (logs error, skips) |
| Table 9 missing (incomplete CSV) | Table 9 not generated; cascades from table 8 wrap to table 8 (existing behavior) |

### 11.3 Logging

All parsing operations use `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` with the prefix `[EncounterParser]` for easy filtering in Unity console.

---

## 12. Testing Strategy

### 12.1 Unit Tests (DiceExpression)

```
TestParse_FixedNumber:       "3"      → NumDice=0, Modifier=3
TestParse_SimpleDice:        "1d3"    → NumDice=1, Sides=3, Modifier=0
TestParse_DiceWithModifier:  "2d4+1"  → NumDice=2, Sides=4, Modifier=1
TestParse_Invalid:           "abc"    → null
TestRoll_FixedValue:         Fixed(3) → always 3
TestRoll_Range:              1d6      → result in [1, 6] over 1000 trials
TestMinMax:                  2d4+1    → Min=3, Max=9
```

### 12.2 Unit Tests (EncounterDescriptionParser)

```
TestParse_SimpleSingle:      "1 darkmantle"         → 1 group, fixed count 1
TestParse_DiceCount:         "1d3 dire rats"         → 1 group, dice count
TestParse_Compound:          "1 ettercap and 1d3+1 Medium monstrous spiders (vermin)"
                              → 2 groups
TestParse_NPC:               "5th-level human monk NPC" → NPC group
TestParse_Cascade:           "Roll on 2nd-level table" → cascade, target=2
TestParse_Annotation:        "1 dretch (demon)"      → annotation="demon"
TestParse_QuotedComma:       "1 ghost, 5th-level fighter" → special case
TestParse_CompoundNPC:       "5th-level hobgoblin fighter NPC and 5th-level goblin rogue NPC"
                              → 2 NPC groups
```

### 12.3 Integration Tests

```
TestBuildFromCSV_AllTablesPresent:  CSV → 9 tables, each with entries
TestBuildFromCSV_CascadeEntries:   Each table has cascade top/bottom
TestBuildFromCSV_TotalEntries:     Total entry count matches CSV row count
TestGenerateEncounter_Level1:      Roll produces valid EncounterDefinition
TestGenerateEncounter_Level9:      New table 9 works correctly
TestGenerateEncounter_DiceRolled:  Counts vary between generations
TestCSVFallback_MissingFile:       Falls back to hardcoded tables
```

### 12.4 Manual QA

1. Run UI with level slider set to 1-9; generate 100 encounters per level
2. Verify creature names resolve correctly (no "unknown" entries)
3. Verify dice-based counts produce expected ranges
4. Verify compound entries spawn multiple creature types
5. Verify cascade logic works at boundaries (levels 1 and 9)

---

## Appendix A: Annotation Tag Reference

| Tag | Meaning | Example Creatures |
|-----|---------|-------------------|
| `(vermin)` | Vermin creature type | Monstrous centipede, monstrous scorpion |
| `(animal)` | Animal creature type | Viper snake, constrictor snake |
| `(demon)` | Demon subtype | Dretch, quasit, babau, vrock |
| `(devil)` | Devil subtype | Imp, lemure, bearded devil, bone devil |
| `(lycanthrope)` | Lycanthrope template | Wererat, werewolf, wereboar, weretiger |
| `(ghoul)` | Ghoul/ghast variant | Ghast, ghoul |
| `(fungus)` | Fungus creature type | Violet fungus, shrieker |
| `(beholder)` | Beholder variant | Gauth |
| `(eladrin)` | Eladrin subtype | Bralani |
| `(genie)` | Genie subtype | Janni, djinni, efreeti |
| `(hag)` | Hag subtype | Green hag, annis |
| `(ooze)` | Ooze creature type | Gelatinous cube, gray ooze, ochre jelly |
| `(slaad)` | Slaad subtype | Red slaad, blue slaad |
| `(pyro- or cryo-)` | Variant hydra (fire or cold) | Pyro-/cryo-hydra |

---

## Appendix B: Complete CSV Pattern Regex Reference

```
PATTERN_CASCADE:    ^Roll on (\d+)(?:st|nd|rd|th)-level table$
PATTERN_NPC:        ^(?:(\d+(?:d\d+(?:[+-]\d+)?)?)\s+)?(\d+)(?:st|nd|rd|th)-level\s+(\w+)\s+(\w+)\s+NPCs?
PATTERN_CREATURE:   ^(\d+(?:d\d+(?:[+-]\d+)?)?)\s+(.+?)(?:\s*\(([^)]+)\))?\s*$
PATTERN_DICE:       ^(\d+)(?:d(\d+)([+-]\d+)?)?$
PATTERN_SPECIAL:    ^(\d+)\s+(.+?),\s*(\d+)(?:st|nd|rd|th)-level\s+(\w+)$
COMPOUND_SPLIT:     \s+and\s+(?=\d)
ORDINAL_SUFFIX:     (?:st|nd|rd|th)
```

---

## Appendix C: CSV Row Count by Level

| Level | Entries | Cascade | Encounters |
|-------|---------|---------|------------|
| 1 | 19 | 1 (to L2) | 18 |
| 2 | 20 | 2 (L1, L3) | 18 |
| 3 | 21 | 2 (L2, L4) | 19 |
| 4 | 22 | 2 (L3, L5) | 20 |
| 5 | 22 | 2 (L4, L6) | 20 |
| 6 | 22 | 2 (L5, L7) | 20 |
| 7 | 22 | 2 (L6, L8) | 20 |
| 8 | 22 | 2 (L7, L9) | 20 |
| 9 | ~22* | 2 (L8, +) | ~20* |
| **Total** | **~192** | **17** | **~175** |

*Table 9 data in current CSV is incomplete; full data available in DMG images.

---

*End of Design Document*
