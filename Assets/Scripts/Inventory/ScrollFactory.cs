using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates scroll ItemData entries for every spell in the SpellDatabase.
/// Follows D&D 3.5e DMG scroll pricing rules:
///   - Price = SpellLevel × CasterLevel × 25 gp  (0-level uses 0.5 for spell level → 12.5 gp, rounded to 13)
///   - Minimum CL = max(1, 2 × SpellLevel - 1)
///   - Arcane scrolls for Wizard/Sorcerer/Bard spells; Divine scrolls for Cleric/Druid/Paladin/Ranger spells
///   - If a spell is on both arcane and divine lists, both scroll versions are generated.
/// </summary>
public static class ScrollFactory
{
    // ── Arcane / Divine class lists ──
    private static readonly string[] ArcaneClasses = { "Wizard", "Sorcerer", "Bard" };
    private static readonly string[] DivineClasses = { "Cleric", "Druid", "Paladin", "Ranger" };

    /// <summary>All scroll IDs registered by this factory (populated after RegisterAllScrolls).</summary>
    private static readonly List<string> _registeredScrollIds = new List<string>();
    public static IReadOnlyList<string> RegisteredScrollIds => _registeredScrollIds;

    private static bool _registered = false;

    // ── Public API ──

    /// <summary>
    /// Registers scroll items in ItemDatabase for every non-placeholder spell in SpellDatabase.
    /// Call this AFTER SpellDatabase.Init() and during ItemDatabase.Init().
    /// </summary>
    public static void RegisterAllScrolls()
    {
        if (_registered) return;
        _registered = true;

        SpellDatabase.Init();
        List<SpellData> allSpells = SpellDatabase.GetAllSpells();

        int arcaneCount = 0, divineCount = 0, skippedCount = 0;

        foreach (SpellData spell in allSpells)
        {
            if (spell == null || string.IsNullOrWhiteSpace(spell.SpellId))
                continue;

            // Skip placeholder spells that have no implemented effect
            if (spell.IsPlaceholder)
            {
                skippedCount++;
                continue;
            }

            bool isArcane = IsSpellArcane(spell);
            bool isDivine = IsSpellDivine(spell);

            if (!isArcane && !isDivine)
            {
                // Spell is on no standard class list — skip (domain-only spells handled below)
                // Check if it's a domain-only spell → treat as divine
                if (spell.AvailableFor != null && spell.AvailableFor.Any(a => !string.IsNullOrWhiteSpace(a.Domain)))
                {
                    isDivine = true;
                }
                else
                {
                    skippedCount++;
                    continue;
                }
            }

            int spellLevel = GetBestSpellLevel(spell);
            if (spellLevel < 0)
            {
                skippedCount++;
                continue;
            }

            int minCasterLevel = GetMinimumCasterLevel(spellLevel);
            int priceGp = CalculateScrollPrice(spellLevel, minCasterLevel);

            if (isArcane)
            {
                RegisterScroll(spell, spellLevel, minCasterLevel, priceGp, "Arcane");
                arcaneCount++;
            }

            if (isDivine)
            {
                RegisterScroll(spell, spellLevel, minCasterLevel, priceGp, "Divine");
                divineCount++;
            }
        }

        Debug.Log($"[ScrollFactory] Registered {arcaneCount} arcane + {divineCount} divine scrolls ({skippedCount} spells skipped).");
    }

    /// <summary>
    /// Adds all registered scrolls to the store, organized by category "Scroll (Lvl X)".
    /// </summary>
    public static void AddScrollsToStore(StoreInventory store)
    {
        if (store == null) return;

        int added = 0;
        foreach (string scrollId in _registeredScrollIds)
        {
            ItemData template = ItemDatabase.Get(scrollId);
            if (template == null) continue;

            string category = $"Scroll (Lvl {template.ScrollSpellLevel})";
            store.AddScrollItem(scrollId, category, template.BasePriceGp);
            added++;
        }

        Debug.Log($"[ScrollFactory] Added {added} scrolls to store.");
    }

    // ── Pricing ──

    /// <summary>
    /// D&D 3.5e DMG: Minimum CL = max(1, 2 × spellLevel - 1).
    /// </summary>
    public static int GetMinimumCasterLevel(int spellLevel)
    {
        if (spellLevel <= 1) return 1;
        return 2 * spellLevel - 1;
    }

    /// <summary>
    /// D&D 3.5e DMG: Price = SL × CL × 25. For 0-level, SL is treated as 0.5 → 12.5 gp → rounded to 13.
    /// </summary>
    public static int CalculateScrollPrice(int spellLevel, int casterLevel)
    {
        if (spellLevel == 0)
            return Mathf.CeilToInt(0.5f * casterLevel * 25f); // 12.5 → 13 gp for CL 1

        return spellLevel * casterLevel * 25;
    }

    // ── Scroll type classification ──

    /// <summary>Returns true if the spell is on any arcane class spell list.</summary>
    public static bool IsSpellArcane(SpellData spell)
    {
        if (spell == null) return false;
        foreach (string cls in ArcaneClasses)
        {
            if (IsSpellOnClassList(spell, cls))
                return true;
        }
        return false;
    }

    /// <summary>Returns true if the spell is on any divine class spell list.</summary>
    public static bool IsSpellDivine(SpellData spell)
    {
        if (spell == null) return false;
        foreach (string cls in DivineClasses)
        {
            if (IsSpellOnClassList(spell, cls))
                return true;
        }
        return false;
    }

    /// <summary>Check if a spell is on the specified class's spell list (ignoring domain).</summary>
    public static bool IsSpellOnClassList(SpellData spell, string className)
    {
        if (spell == null || string.IsNullOrWhiteSpace(className))
            return false;

        // Check modern AvailableFor list (non-domain entries)
        if (spell.AvailableFor != null)
        {
            foreach (var a in spell.AvailableFor)
            {
                if (a != null && a.MatchesClass(className) && string.IsNullOrWhiteSpace(a.Domain))
                    return true;
            }
        }

        // Fallback: legacy ClassList
        if (spell.ClassList != null)
        {
            foreach (string cls in spell.ClassList)
            {
                if (string.Equals(cls, className, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the "best" (lowest) spell level across primary caster classes (Wizard/Cleric).
    /// Falls back to the spell's default SpellLevel.
    /// </summary>
    public static int GetBestSpellLevel(SpellData spell)
    {
        if (spell == null) return -1;

        int best = int.MaxValue;
        string[] primaryClasses = { "Wizard", "Cleric", "Druid", "Sorcerer", "Bard", "Paladin", "Ranger" };

        foreach (string cls in primaryClasses)
        {
            int level = spell.GetSpellLevelFor(cls);
            if (level >= 0 && level < best)
                best = level;
        }

        if (best == int.MaxValue)
        {
            // Check domain availability
            if (spell.AvailableFor != null)
            {
                foreach (var a in spell.AvailableFor)
                {
                    if (a != null && a.Level >= 0 && a.Level < best)
                        best = a.Level;
                }
            }
        }

        return best == int.MaxValue ? spell.SpellLevel : best;
    }

    /// <summary>
    /// Get the spell level for a specific class on this scroll's spell.
    /// Returns -1 if the spell is not on that class's list.
    /// </summary>
    public static int GetSpellLevelForClass(SpellData spell, string className)
    {
        if (spell == null || string.IsNullOrWhiteSpace(className))
            return -1;
        return spell.GetSpellLevelFor(className);
    }

    // ── Internal registration ──

    private static void RegisterScroll(SpellData spell, int spellLevel, int casterLevel, int priceGp, string scrollType)
    {
        string id = GenerateScrollId(spell.SpellId, scrollType);
        string name = $"Scroll of {spell.Name} ({scrollType})";
        string description = BuildScrollDescription(spell, spellLevel, casterLevel, priceGp, scrollType);

        // Pick icon color: cyan for arcane, gold for divine
        Color iconColor = scrollType == "Arcane"
            ? new Color(0.4f, 0.7f, 1f)   // Light blue for arcane
            : new Color(1f, 0.85f, 0.4f);  // Gold for divine

        ItemData scrollItem = new ItemData
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            ConsumableEffect = ConsumableEffectType.SpellEffect,
            ConsumableSpellName = spell.Name,
            ConsumableMinimumCasterLevel = casterLevel,
            ConsumableModifier = 0,
            BasePriceGp = priceGp,
            WeightLbs = 0.1f,
            IconChar = "\u2709", // ✉ scroll/document icon (Unicode safe for Unity Text)
            IconColor = iconColor,
            IsScroll = true,
            ScrollType = scrollType,
            ScrollSpellLevel = spellLevel
        };

        ItemDatabase.RegisterScrollItem(scrollItem);
        _registeredScrollIds.Add(id);
    }

    /// <summary>Generate a unique scroll item ID from spell ID and type.</summary>
    public static string GenerateScrollId(string spellId, string scrollType)
    {
        // Normalize: "Cure Light Wounds" → "cure_light_wounds", type "Arcane" → "arcane"
        string normalizedSpell = (spellId ?? "unknown").Trim().ToLowerInvariant().Replace(" ", "_");
        string normalizedType = (scrollType ?? "arcane").Trim().ToLowerInvariant();
        return $"scroll_{normalizedType}_{normalizedSpell}";
    }

    private static string BuildScrollDescription(SpellData spell, int spellLevel, int casterLevel, int priceGp, string scrollType)
    {
        string schoolText = !string.IsNullOrWhiteSpace(spell.School) ? $" [{spell.School}]" : "";
        string classList = BuildClassListString(spell, scrollType);

        return $"{scrollType} scroll containing {spell.Name}{schoolText}.\n" +
               $"Spell Level: {spellLevel} | Caster Level: {casterLevel} | Price: {priceGp} gp\n" +
               $"Classes: {classList}\n" +
               $"Using a scroll is a spell completion action that provokes AoO.\n" +
               $"Requires: spell on your class list, ability score ≥ {10 + spellLevel}, " +
               $"and caster level check if your CL < {casterLevel}.\n" +
               $"Use Magic Device (DC {20 + casterLevel}) can bypass requirements.";
    }

    private static string BuildClassListString(SpellData spell, string scrollType)
    {
        List<string> classes = new List<string>();
        string[] checkClasses = scrollType == "Arcane" ? ArcaneClasses : DivineClasses;

        foreach (string cls in checkClasses)
        {
            int level = spell.GetSpellLevelFor(cls);
            if (level >= 0)
                classes.Add($"{cls} {level}");
        }

        return classes.Count > 0 ? string.Join(", ", classes) : scrollType;
    }
}
