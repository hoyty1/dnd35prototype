using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// One creature group parsed from an encounter description string.
/// Represents a single "block" like "1d3+1 Medium monstrous spiders (vermin)"
/// or "5th-level human monk NPC".
///
/// Phase 5: Random Encounter Generator.
/// </summary>
[Serializable]
public class ParsedCreatureGroup
{
    /// <summary>Dice expression for creature count (e.g., "1d3+1" or fixed "1").</summary>
    public DiceExpression CountExpression;

    /// <summary>
    /// Normalized creature name with annotation stripped.
    /// For standard creatures: "Medium monstrous spiders" (annotation removed).
    /// For NPCs: constructed as "{race}" (e.g., "human", "kobold").
    /// </summary>
    public string CreatureName = "";

    /// <summary>
    /// Parenthetical annotation if present (e.g., "vermin", "lycanthrope", "demon").
    /// Null if no annotation was found.
    /// </summary>
    public string Annotation;

    /// <summary>NPC class name if this is a classed NPC entry (e.g., "Monk", "Fighter").</summary>
    public string NpcClass;

    /// <summary>NPC class level if this is a classed NPC entry (e.g., 5).</summary>
    public int NpcLevel;

    /// <summary>NPC/creature race for classed entries (e.g., "human", "kobold", "ogre").</summary>
    public string NpcRace;

    /// <summary>Whether this group is a class-leveled NPC.</summary>
    public bool IsNpc => !string.IsNullOrEmpty(NpcClass) && NpcLevel > 0;

    /// <summary>Optional creature template IDs (e.g., "fiendish", "celestial", "half-dragon").</summary>
    public List<string> TemplateIds;

    /// <summary>Whether this group has creature templates applied.</summary>
    public bool HasTemplates => TemplateIds != null && TemplateIds.Count > 0;

    /// <summary>Any additional modifiers or notes (e.g., "dominated", "with crocodile").</summary>
    public string Notes;

    /// <summary>The raw text this group was parsed from, for debugging.</summary>
    public string RawText = "";

    /// <summary>
    /// Get a human-readable summary of this group.
    /// </summary>
    public override string ToString()
    {
        string countStr = CountExpression != null ? CountExpression.ToString() : "?";
        if (IsNpc)
            return $"{countStr}x {NpcRace} {NpcClass} {NpcLevel} NPC";
        string name = CreatureName;
        if (!string.IsNullOrEmpty(Annotation))
            name += $" ({Annotation})";
        return $"{countStr}x {name}";
    }
}

/// <summary>
/// Complete parsed result from one encounter description string.
/// May contain one or more creature groups (compound entries joined by "and").
///
/// Phase 5: Random Encounter Generator.
/// </summary>
[Serializable]
public class ParsedEncounterDescription
{
    /// <summary>Original description string from the CSV.</summary>
    public string RawDescription = "";

    /// <summary>Whether this is a cascade entry ("Roll on Xth-level table").</summary>
    public bool IsCascade;

    /// <summary>Target table level for cascade entries (e.g., 2 for "Roll on 2nd-level table").</summary>
    public int CascadeTargetLevel;

    /// <summary>
    /// Parsed creature groups. Empty for cascade entries.
    /// Compound entries (e.g., "1 ettercap and 1d3+1 spiders") produce multiple groups.
    /// </summary>
    public List<ParsedCreatureGroup> Groups = new List<ParsedCreatureGroup>();

    /// <summary>Whether parsing encountered any issues.</summary>
    public bool HasWarnings;

    /// <summary>Warning messages from parsing (non-fatal issues).</summary>
    public List<string> Warnings = new List<string>();

    /// <summary>Add a warning message.</summary>
    public void AddWarning(string msg)
    {
        HasWarnings = true;
        Warnings.Add(msg);
    }

    /// <summary>Total number of creature groups parsed.</summary>
    public int GroupCount => Groups.Count;

    /// <summary>
    /// Get a human-readable summary of this parsed result.
    /// </summary>
    public override string ToString()
    {
        if (IsCascade)
            return $"Cascade → level {CascadeTargetLevel}";
        if (Groups.Count == 0)
            return $"[Empty] {RawDescription}";
        var parts = new List<string>();
        for (int i = 0; i < Groups.Count; i++)
            parts.Add(Groups[i].ToString());
        return string.Join(" + ", parts);
    }
}

/// <summary>
/// Static utility class that parses encounter description strings from the
/// dungeon_encounters.csv into structured <see cref="ParsedEncounterDescription"/>
/// objects.
///
/// Handles all DMG 3.5e encounter description patterns:
///   1. Cascade:   "Roll on 2nd-level table"
///   2. Simple:    "1 darkmantle"
///   3. Dice:      "1d3 dire rats"
///   4. Compound:  "1 ettercap and 1d3+1 Medium monstrous spiders (vermin)"
///   5. NPC:       "5th-level human monk NPC"
///   6. Annotated: "1 dretch (demon)"
///   7. Special:   "1 ghost, 5th-level fighter"
///
/// Phase 5: Random Encounter Generator.
/// </summary>
public static class EncounterDescriptionParser
{
    // =========================================================================
    //  Compiled Regex Patterns
    // =========================================================================

    /// <summary>Cascade pattern: "Roll on Nth-level table".</summary>
    private static readonly Regex CascadePattern = new Regex(
        @"^Roll on (\d+)(?:st|nd|rd|th)-level table$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// NPC pattern: "[count] Nth-level {race} {class} NPC[s]"
    /// Captures: count (opt), level, race, class.
    /// Examples:
    ///   "5th-level human monk NPC"
    ///   "1d3 5th-level troglodyte cleric NPCs"
    /// </summary>
    private static readonly Regex NpcPattern = new Regex(
        @"^(?:(\d+(?:d\d+(?:[+-]\d+)?)?)\s+)?(\d+)(?:st|nd|rd|th)-level\s+(\w+)\s+(\w+)\s+NPCs?(?:\s+.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Dominated NPC pattern: "[count] dominated Nth-level {race} {class} NPC[s]"
    /// Same as NPC pattern but with "dominated" prefix.
    /// Example: "1 dominated 5th-level human barbarian NPC"
    /// </summary>
    private static readonly Regex DominatedNpcPattern = new Regex(
        @"^(?:(\d+(?:d\d+(?:[+-]\d+)?)?)\s+)?dominated\s+(\d+)(?:st|nd|rd|th)-level\s+(\w+)\s+(\w+)\s+NPCs?(?:\s+.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Special comma pattern: "{count} {creature}, Nth-level {class}"
    /// Examples:
    ///   "1 ghost, 5th-level fighter"
    ///   "1 vampire, 5th-level fighter"
    ///   "1 ogre barbarian, 4th level"
    /// </summary>
    private static readonly Regex SpecialCommaClassPattern = new Regex(
        @"^(\d+)\s+(.+?),\s*(\d+)(?:st|nd|rd|th)-level\s+(\w+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Alternate comma pattern: "{count} {creature} {class}, Nth level"
    /// Example: "1 ogre barbarian, 4th level"
    /// </summary>
    private static readonly Regex SpecialCommaLevelPattern = new Regex(
        @"^(\d+)\s+(\w+)\s+(\w+),\s*(\d+)(?:st|nd|rd|th)\s+level$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Half-dragon pattern: "{count} half-dragon Nth-level {class}"
    /// Example: "1 half-dragon 4th-level fighter"
    /// </summary>
    private static readonly Regex HalfDragonPattern = new Regex(
        @"^(\d+)\s+half-dragon\s+(\d+)(?:st|nd|rd|th)-level\s+(\w+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Devils comma pattern: "{count} devils, {subtype}"
    /// Example: "1d3 devils, hellcat"
    /// </summary>
    private static readonly Regex DevilsCommaPattern = new Regex(
        @"^(\d+(?:d\d+(?:[+-]\d+)?)?)\s+devils?,\s*(\w+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Standard creature pattern: "{dice_expr} {creature_name} [(annotation)]"
    /// The workhorse pattern that handles most entries.
    /// Captures: count_expression, creature_name, annotation (optional).
    /// </summary>
    private static readonly Regex StandardCreaturePattern = new Regex(
        @"^(\d+(?:d\d+(?:[+-]\d+)?)?)\s+(.+?)(?:\s*\(([^)]+)\))?\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Compound entry splitter: split on " and " only when followed by a digit.
    /// This prevents splitting "bugbear" on embedded "and" while correctly
    /// splitting "1 ettercap and 1d3+1 spiders".
    /// </summary>
    private static readonly Regex CompoundSplitter = new Regex(
        @"\s+and\s+(?=\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parenthetical annotation pattern for stripping from names.
    /// Matches trailing "(vermin)", "(demon)", "(pyro- or cryo-)", etc.
    /// </summary>
    private static readonly Regex AnnotationPattern = new Regex(
        @"\s*\(([^)]+)\)\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// NPC companion annotation: "(with crocodile)", "(with animal companion)".
    /// </summary>
    private static readonly Regex CompanionPattern = new Regex(
        @"\s*\(with\s+(.+?)\)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // =========================================================================
    //  Main Parse Method
    // =========================================================================

    /// <summary>
    /// Parse a complete encounter description string from the CSV.
    /// Handles cascade entries, compound entries, NPC entries, annotations, and
    /// all special patterns.
    /// </summary>
    /// <param name="description">Raw encounter description from CSV.</param>
    /// <returns>Structured parse result with creature groups.</returns>
    public static ParsedEncounterDescription Parse(string description)
    {
        var result = new ParsedEncounterDescription { RawDescription = description ?? "" };

        if (string.IsNullOrWhiteSpace(description))
        {
            result.AddWarning("Empty encounter description");
            return result;
        }

        description = description.Trim();

        // ── Step 1: Check for cascade pattern ──
        Match cascadeMatch = CascadePattern.Match(description);
        if (cascadeMatch.Success)
        {
            result.IsCascade = true;
            result.CascadeTargetLevel = int.Parse(cascadeMatch.Groups[1].Value);
            return result;
        }

        // ── Step 2: Split compound entries on " and " ──
        // But first check if this is a quoted CSV field that uses commas
        // (description may already be unquoted by CSV parser)
        string[] groups = SplitCompoundEntry(description);

        // ── Step 3: Parse each group ──
        for (int i = 0; i < groups.Length; i++)
        {
            string groupText = groups[i].Trim();
            if (string.IsNullOrWhiteSpace(groupText)) continue;

            ParsedCreatureGroup group = ParseSingleGroup(groupText);
            if (group != null)
            {
                result.Groups.Add(group);
            }
            else
            {
                result.AddWarning($"Could not parse group: '{groupText}'");
            }
        }

        if (result.Groups.Count == 0 && !result.IsCascade)
        {
            result.AddWarning($"No groups parsed from: '{description}'");
        }

        return result;
    }

    // =========================================================================
    //  Compound Entry Splitting
    // =========================================================================

    /// <summary>
    /// Split a compound encounter description on " and " boundaries.
    /// Only splits when the text after "and" starts with a digit, indicating
    /// a new creature group count expression.
    /// </summary>
    /// <param name="description">Full encounter description.</param>
    /// <returns>Array of individual group strings.</returns>
    private static string[] SplitCompoundEntry(string description)
    {
        string[] parts = CompoundSplitter.Split(description);
        if (parts.Length == 0) return new[] { description };
        return parts;
    }

    // =========================================================================
    //  Single Group Parsing
    // =========================================================================

    /// <summary>
    /// Parse a single creature group string. Tries patterns in priority order:
    ///   1. Dominated NPC (e.g., "1 dominated 5th-level human barbarian NPC")
    ///   2. NPC entry (e.g., "5th-level human monk NPC")
    ///   3. Half-dragon (e.g., "1 half-dragon 4th-level fighter")
    ///   4. Devils comma variant (e.g., "1d3 devils, hellcat")
    ///   5. Special comma + class (e.g., "1 ghost, 5th-level fighter")
    ///   6. Special comma + level (e.g., "1 ogre barbarian, 4th level")
    ///   7. Standard creature (e.g., "1d3+1 Medium monstrous spiders (vermin)")
    ///   8. Fallback: entire string as description, count=1
    /// </summary>
    /// <param name="text">Single group text to parse.</param>
    /// <returns>Parsed creature group, or null if completely unparseable.</returns>
    private static ParsedCreatureGroup ParseSingleGroup(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();

        ParsedCreatureGroup group;

        // ── Pattern 1: Dominated NPC ──
        group = TryParseDominatedNpc(text);
        if (group != null) return group;

        // ── Pattern 2: NPC entry ──
        group = TryParseNpcEntry(text);
        if (group != null) return group;

        // ── Pattern 3: Half-dragon ──
        group = TryParseHalfDragon(text);
        if (group != null) return group;

        // ── Pattern 4: Devils comma variant ──
        group = TryParseDevilsComma(text);
        if (group != null) return group;

        // ── Pattern 5: Special comma + class ──
        group = TryParseSpecialCommaClass(text);
        if (group != null) return group;

        // ── Pattern 6: Special comma + level ──
        group = TryParseSpecialCommaLevel(text);
        if (group != null) return group;

        // ── Pattern 7: Standard creature ──
        group = TryParseStandardCreature(text);
        if (group != null) return group;

        // ── Pattern 8: Fallback ──
        return CreateFallbackGroup(text);
    }

    // =========================================================================
    //  Pattern Parsers
    // =========================================================================

    /// <summary>
    /// Try to parse an NPC entry like "5th-level human monk NPC" or
    /// "1d3 5th-level troglodyte cleric NPCs".
    /// </summary>
    private static ParsedCreatureGroup TryParseNpcEntry(string text)
    {
        Match match = NpcPattern.Match(text);
        if (!match.Success) return null;

        string countStr = match.Groups[1].Success ? match.Groups[1].Value : "1";
        int level = int.Parse(match.Groups[2].Value);
        string race = match.Groups[3].Value.ToLowerInvariant();
        string className = match.Groups[4].Value;

        // Extract companion annotation if present
        string notes = null;
        Match companionMatch = CompanionPattern.Match(text);
        if (companionMatch.Success)
        {
            notes = "with " + companionMatch.Groups[1].Value;
        }

        return new ParsedCreatureGroup
        {
            CountExpression = DiceExpression.Parse(countStr) ?? new DiceExpression(0, 0, 1, "1"),
            CreatureName = race,
            NpcRace = race,
            NpcClass = CapitalizeFirst(className),
            NpcLevel = level,
            Notes = notes,
            RawText = text
        };
    }

    /// <summary>
    /// Try to parse a dominated NPC entry like "1 dominated 5th-level human barbarian NPC".
    /// </summary>
    private static ParsedCreatureGroup TryParseDominatedNpc(string text)
    {
        Match match = DominatedNpcPattern.Match(text);
        if (!match.Success) return null;

        string countStr = match.Groups[1].Success ? match.Groups[1].Value : "1";
        int level = int.Parse(match.Groups[2].Value);
        string race = match.Groups[3].Value.ToLowerInvariant();
        string className = match.Groups[4].Value;

        return new ParsedCreatureGroup
        {
            CountExpression = DiceExpression.Parse(countStr) ?? new DiceExpression(0, 0, 1, "1"),
            CreatureName = race,
            NpcRace = race,
            NpcClass = CapitalizeFirst(className),
            NpcLevel = level,
            Notes = "dominated",
            RawText = text
        };
    }

    /// <summary>
    /// Try to parse a half-dragon entry like "1 half-dragon 4th-level fighter".
    /// </summary>
    private static ParsedCreatureGroup TryParseHalfDragon(string text)
    {
        Match match = HalfDragonPattern.Match(text);
        if (!match.Success) return null;

        int count = int.Parse(match.Groups[1].Value);
        int level = int.Parse(match.Groups[2].Value);
        string className = match.Groups[3].Value;

        return new ParsedCreatureGroup
        {
            CountExpression = new DiceExpression(0, 0, count, count.ToString()),
            CreatureName = "human", // base race assumed human for half-dragon
            NpcRace = "human",
            NpcClass = CapitalizeFirst(className),
            NpcLevel = level,
            TemplateIds = new List<string> { "half-dragon" },
            RawText = text
        };
    }

    /// <summary>
    /// Try to parse a devils comma variant like "1d3 devils, hellcat".
    /// This pattern specifies a count and a creature subtype variant.
    /// </summary>
    private static ParsedCreatureGroup TryParseDevilsComma(string text)
    {
        Match match = DevilsCommaPattern.Match(text);
        if (!match.Success) return null;

        string countStr = match.Groups[1].Value;
        string subtype = match.Groups[2].Value.ToLowerInvariant();

        return new ParsedCreatureGroup
        {
            CountExpression = DiceExpression.Parse(countStr) ?? new DiceExpression(0, 0, 1, "1"),
            CreatureName = subtype,  // "hellcat" is the actual creature
            Annotation = "devil",
            RawText = text
        };
    }

    /// <summary>
    /// Try to parse a special comma + class pattern like "1 ghost, 5th-level fighter".
    /// The creature has class levels expressed after a comma.
    /// </summary>
    private static ParsedCreatureGroup TryParseSpecialCommaClass(string text)
    {
        Match match = SpecialCommaClassPattern.Match(text);
        if (!match.Success) return null;

        int count = int.Parse(match.Groups[1].Value);
        string creature = match.Groups[2].Value.Trim();
        int level = int.Parse(match.Groups[3].Value);
        string className = match.Groups[4].Value;

        // Strip annotation from creature name
        string annotation = null;
        Match annMatch = AnnotationPattern.Match(creature);
        if (annMatch.Success)
        {
            annotation = annMatch.Groups[1].Value;
            creature = creature.Substring(0, annMatch.Index).Trim();
        }

        return new ParsedCreatureGroup
        {
            CountExpression = new DiceExpression(0, 0, count, count.ToString()),
            CreatureName = creature.ToLowerInvariant(),
            NpcRace = creature.ToLowerInvariant(),
            NpcClass = CapitalizeFirst(className),
            NpcLevel = level,
            Annotation = annotation,
            RawText = text
        };
    }

    /// <summary>
    /// Try to parse the alternate comma level pattern: "1 ogre barbarian, 4th level".
    /// The creature has a class name before the comma and the level after.
    /// </summary>
    private static ParsedCreatureGroup TryParseSpecialCommaLevel(string text)
    {
        Match match = SpecialCommaLevelPattern.Match(text);
        if (!match.Success) return null;

        int count = int.Parse(match.Groups[1].Value);
        string creature = match.Groups[2].Value.ToLowerInvariant();
        string className = match.Groups[3].Value;
        int level = int.Parse(match.Groups[4].Value);

        return new ParsedCreatureGroup
        {
            CountExpression = new DiceExpression(0, 0, count, count.ToString()),
            CreatureName = creature,
            NpcRace = creature,
            NpcClass = CapitalizeFirst(className),
            NpcLevel = level,
            RawText = text
        };
    }

    /// <summary>
    /// Parse a standard creature entry: "{dice_expr} {creature_name} [(annotation)]".
    /// This is the most common pattern, handling entries like:
    ///   "1 darkmantle"
    ///   "1d3 dire rats"
    ///   "1d3+1 Medium monstrous spiders (vermin)"
    ///   "1 dretch (demon)"
    /// </summary>
    private static ParsedCreatureGroup TryParseStandardCreature(string text)
    {
        Match match = StandardCreaturePattern.Match(text);
        if (!match.Success) return null;

        string countStr = match.Groups[1].Value;
        string creatureName = match.Groups[2].Value.Trim();
        string annotation = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;

        // Handle companion annotations like "(with crocodile)"
        string notes = null;
        if (annotation != null && annotation.StartsWith("with ", StringComparison.OrdinalIgnoreCase))
        {
            notes = annotation;
            annotation = null;
        }

        DiceExpression count = DiceExpression.Parse(countStr);
        if (count == null)
        {
            Debug.LogWarning($"[EncounterParser] Could not parse count '{countStr}' in '{text}'");
            return null;
        }

        return new ParsedCreatureGroup
        {
            CountExpression = count,
            CreatureName = creatureName.ToLowerInvariant(),
            Annotation = annotation,
            Notes = notes,
            RawText = text
        };
    }

    /// <summary>
    /// Create a fallback group when no pattern matches.
    /// Uses count=1 and the entire text as the creature name.
    /// </summary>
    private static ParsedCreatureGroup CreateFallbackGroup(string text)
    {
        Debug.LogWarning($"[EncounterParser] Fallback parse for: '{text}'");
        return new ParsedCreatureGroup
        {
            CountExpression = new DiceExpression(0, 0, 1, "1"),
            CreatureName = text.ToLowerInvariant(),
            RawText = text
        };
    }

    // =========================================================================
    //  Batch Parsing / Validation
    // =========================================================================

    /// <summary>
    /// Parse all encounter descriptions from a list of raw strings.
    /// Returns a list of parse results with summary statistics.
    /// Useful for testing and validation.
    /// </summary>
    /// <param name="descriptions">Encounter description strings.</param>
    /// <returns>List of parsed results.</returns>
    public static List<ParsedEncounterDescription> ParseAll(IEnumerable<string> descriptions)
    {
        var results = new List<ParsedEncounterDescription>();
        foreach (string desc in descriptions)
        {
            results.Add(Parse(desc));
        }
        return results;
    }

    /// <summary>
    /// Run a validation pass over parsed results and log a summary.
    /// Returns the number of entries with warnings.
    /// </summary>
    /// <param name="results">Parsed results to validate.</param>
    /// <returns>Number of entries with warnings.</returns>
    public static int ValidateAndLog(List<ParsedEncounterDescription> results)
    {
        int cascadeCount = 0;
        int normalCount = 0;
        int warningCount = 0;
        int totalGroups = 0;
        int npcGroups = 0;
        int compoundEntries = 0;

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            if (r.IsCascade)
            {
                cascadeCount++;
                continue;
            }

            normalCount++;
            totalGroups += r.Groups.Count;

            if (r.Groups.Count > 1)
                compoundEntries++;

            for (int g = 0; g < r.Groups.Count; g++)
            {
                if (r.Groups[g].IsNpc)
                    npcGroups++;
            }

            if (r.HasWarnings)
            {
                warningCount++;
                for (int w = 0; w < r.Warnings.Count; w++)
                {
                    Debug.LogWarning($"[EncounterParser] Row {i}: {r.Warnings[w]}");
                }
            }
        }

        Debug.Log($"[EncounterParser] Validation Summary:\n" +
                  $"  Total entries: {results.Count}\n" +
                  $"  Cascade: {cascadeCount}\n" +
                  $"  Normal encounters: {normalCount}\n" +
                  $"  Total creature groups: {totalGroups}\n" +
                  $"  Compound entries: {compoundEntries}\n" +
                  $"  NPC groups: {npcGroups}\n" +
                  $"  Entries with warnings: {warningCount}");

        return warningCount;
    }

    // =========================================================================
    //  Utility Methods
    // =========================================================================

    /// <summary>
    /// Capitalize the first letter of a string (e.g., "fighter" → "Fighter").
    /// Used to normalize class names for TemplateClass field.
    /// </summary>
    /// <param name="s">Input string.</param>
    /// <returns>String with first letter capitalized.</returns>
    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length == 1) return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }

    /// <summary>
    /// Strip a trailing parenthetical annotation from a creature name.
    /// E.g., "dire rat (vermin)" → ("dire rat", "vermin").
    /// </summary>
    /// <param name="name">Creature name possibly with annotation.</param>
    /// <param name="annotation">Extracted annotation, or null.</param>
    /// <returns>Name with annotation stripped.</returns>
    public static string StripAnnotation(string name, out string annotation)
    {
        annotation = null;
        if (string.IsNullOrEmpty(name)) return name;

        Match match = AnnotationPattern.Match(name);
        if (match.Success)
        {
            annotation = match.Groups[1].Value.Trim();
            return name.Substring(0, match.Index).Trim();
        }
        return name.Trim();
    }

    /// <summary>
    /// Attempt basic plural → singular normalization for creature names.
    /// Handles common English plural patterns used in the DMG tables.
    /// </summary>
    /// <param name="name">Creature name (possibly plural).</param>
    /// <returns>Depluralized name.</returns>
    public static string Depluralize(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Special cases from DMG tables
        if (name.EndsWith("fungi", StringComparison.OrdinalIgnoreCase))
            return name.Substring(0, name.Length - 1) + "us"; // fungi → fungus
        if (name.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && name.Length > 4)
            return name.Substring(0, name.Length - 3) + "y"; // harpies → harpy
        if (name.EndsWith("ves", StringComparison.OrdinalIgnoreCase) && name.Length > 4)
            return name.Substring(0, name.Length - 3) + "f"; // wolves → wolf (not used in DMG but general)
        if (name.EndsWith("es", StringComparison.OrdinalIgnoreCase) && name.Length > 3)
        {
            // Check if base form exists: "mummies" → "mummy" (handled by -ies above)
            // For "oozes" → "ooze", "gnolls" → won't match
            string withoutEs = name.Substring(0, name.Length - 2);
            // Don't strip -es from names that need it
            if (!withoutEs.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                return withoutEs;
        }
        if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase) && name.Length > 2)
        {
            // Don't strip 's' from names ending in 'ss' (e.g., "cockatrice" isn't "cockatrics")
            if (!name.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith("us", StringComparison.OrdinalIgnoreCase))
            {
                return name.Substring(0, name.Length - 1);
            }
        }

        return name;
    }
}
