// ============================================================================
// D&D 3.5e Item Creation Feats - Spell Source Info
// Tracks which party member provides each required spell for crafting
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// How a required spell is being provided for a crafting project.
/// </summary>
public enum SpellSourceType
{
    /// <summary>The crafter knows/has prepared this spell.</summary>
    CrafterKnown,

    /// <summary>Another party member knows/has prepared this spell and can assist.</summary>
    PartyMemberKnown,

    /// <summary>No one in the party has the spell; a scroll can be purchased to substitute.</summary>
    ScrollSubstitute,

    /// <summary>No one has the spell and no scroll substitute is being used. +5 DC penalty applies.</summary>
    Missing
}

/// <summary>
/// Information about a single required spell's source for crafting.
/// </summary>
[System.Serializable]
public class SpellSource
{
    /// <summary>The spell ID (e.g., "shield_of_faith").</summary>
    public string SpellId;

    /// <summary>The display name of the spell.</summary>
    public string SpellName;

    /// <summary>How this spell is being provided.</summary>
    public SpellSourceType SourceType;

    /// <summary>Name of the character providing this spell (crafter or party member). Null if missing/scroll.</summary>
    public string ProviderName;

    /// <summary>
    /// If SourceType == ScrollSubstitute, the cost of the scroll in gp.
    /// DMG p.238: Scroll cost = spell level × caster level × 25 gp.
    /// </summary>
    public int ScrollCostGp;

    /// <summary>The spell level (for pricing and display).</summary>
    public int SpellLevel;

    /// <summary>User-friendly status line for the UI.</summary>
    public string GetDisplayLine()
    {
        switch (SourceType)
        {
            case SpellSourceType.CrafterKnown:
                return $"✅ {SpellName} (You know it)";
            case SpellSourceType.PartyMemberKnown:
                return $"✅ {SpellName} ({ProviderName} can cast)";
            case SpellSourceType.ScrollSubstitute:
                return $"📜 {SpellName} (Scroll: +{ScrollCostGp:N0} gp)";
            case SpellSourceType.Missing:
                return $"❌ {SpellName} (Missing — +5 DC)";
            default:
                return $"? {SpellName}";
        }
    }
}

/// <summary>
/// Complete spell availability analysis for a crafting project.
/// Built by CraftingValidator.CheckSpellSources() using the full party.
/// </summary>
[System.Serializable]
public class SpellAvailabilityInfo
{
    /// <summary>Per-spell source information.</summary>
    public List<SpellSource> Sources = new List<SpellSource>();

    /// <summary>True if all required spells are covered (by crafter, party, or scroll).</summary>
    public bool AllSpellsCovered => MissingCount == 0;

    /// <summary>True if all required spells are known by crafter or party (no scrolls needed).</summary>
    public bool AllSpellsKnown => Sources.All(s =>
        s.SourceType == SpellSourceType.CrafterKnown || s.SourceType == SpellSourceType.PartyMemberKnown);

    /// <summary>Number of spells that are completely missing (not covered by anyone or scroll).</summary>
    public int MissingCount => Sources.Count(s => s.SourceType == SpellSourceType.Missing);

    /// <summary>Number of spells requiring scroll substitution.</summary>
    public int ScrollCount => Sources.Count(s => s.SourceType == SpellSourceType.ScrollSubstitute);

    /// <summary>Total gold cost of all scroll substitutes.</summary>
    public int TotalScrollCostGp => Sources.Where(s => s.SourceType == SpellSourceType.ScrollSubstitute).Sum(s => s.ScrollCostGp);

    /// <summary>Spells that are truly missing (no source, no scroll). Each adds +5 Spellcraft DC.</summary>
    public List<SpellSource> TrulyMissingSpells => Sources.Where(s => s.SourceType == SpellSourceType.Missing).ToList();

    /// <summary>The Spellcraft DC from truly missing spells (base 5 + 5 per missing).</summary>
    public int SpellcraftDC
    {
        get
        {
            int missing = MissingCount;
            return missing > 0 ? CraftingConstants.BaseCraftingDC + (missing * CraftingConstants.MissingSpellDCIncrease) : 0;
        }
    }

    /// <summary>
    /// Recalculate sources: convert Missing → ScrollSubstitute for spells the user opts to cover with scrolls.
    /// </summary>
    public void EnableScrollSubstitution(bool enabled)
    {
        foreach (var source in Sources)
        {
            if (enabled && source.SourceType == SpellSourceType.Missing && source.ScrollCostGp > 0)
            {
                source.SourceType = SpellSourceType.ScrollSubstitute;
            }
            else if (!enabled && source.SourceType == SpellSourceType.ScrollSubstitute)
            {
                source.SourceType = SpellSourceType.Missing;
            }
        }
    }
}
