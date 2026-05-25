// ============================================================================
// D&D 3.5e Ranger Combat Style System (PHB p.48)
// At level 2, Ranger selects Archery or Two-Weapon Fighting style.
// Bonus feats granted WITHOUT prerequisites at L2, L6, L11.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ranger combat style choice (permanent, selected at level 2).</summary>
public enum RangerCombatStyle
{
    None = 0,
    Archery,
    TwoWeaponFighting
}

/// <summary>
/// Tracks the Ranger's combat style and grants appropriate bonus feats.
/// D&D 3.5e PHB p.48: These feats are granted without meeting prerequisites,
/// but only function while wearing light or no armor.
/// </summary>
public class CombatStyleData
{
    public RangerCombatStyle Style { get; private set; } = RangerCombatStyle.None;
    public bool HasStyleSelected => Style != RangerCombatStyle.None;

    /// <summary>Feats already granted by the combat style.</summary>
    public List<string> GrantedFeats { get; private set; } = new List<string>();

    /// <summary>
    /// Select a combat style. Can only be done once (permanent choice at level 2).
    /// </summary>
    public bool SelectStyle(RangerCombatStyle style)
    {
        if (HasStyleSelected)
        {
            Debug.LogWarning("[CombatStyle] Style already selected — cannot change.");
            return false;
        }

        if (style == RangerCombatStyle.None) return false;

        Style = style;
        Debug.Log($"[CombatStyle] Selected: {style}");
        return true;
    }

    /// <summary>
    /// Grant the appropriate combat style feat for the given ranger level.
    /// Called during level-up. Feats are granted without prerequisites.
    /// </summary>
    /// <param name="rangerLevel">Current Ranger class level</param>
    /// <returns>Feat name granted, or null if no feat at this level</returns>
    public string GrantStyleFeat(int rangerLevel)
    {
        if (!HasStyleSelected) return null;

        string feat = GetFeatForLevel(rangerLevel);
        if (feat == null) return null;

        if (GrantedFeats.Contains(feat))
        {
            Debug.Log($"[CombatStyle] Already has {feat}, skipping.");
            return null;
        }

        GrantedFeats.Add(feat);
        Debug.Log($"[CombatStyle] Granted {feat} at Ranger level {rangerLevel}");
        return feat;
    }

    /// <summary>
    /// Get the feat that should be granted at a specific ranger level for the selected style.
    /// </summary>
    public string GetFeatForLevel(int rangerLevel)
    {
        if (Style == RangerCombatStyle.Archery)
        {
            switch (rangerLevel)
            {
                case 2: return "Rapid Shot";
                case 6: return "Manyshot";
                case 11: return "Improved Precise Shot";
                default: return null;
            }
        }
        else if (Style == RangerCombatStyle.TwoWeaponFighting)
        {
            switch (rangerLevel)
            {
                case 2: return "Two-Weapon Fighting";
                case 6: return "Improved Two-Weapon Fighting";
                case 11: return "Greater Two-Weapon Fighting";
                default: return null;
            }
        }
        return null;
    }

    /// <summary>Whether a given ranger level grants a combat style feat.</summary>
    public static bool IsStyleFeatLevel(int rangerLevel)
    {
        return rangerLevel == 2 || rangerLevel == 6 || rangerLevel == 11;
    }

    /// <summary>Get all feats that should be granted up to and including the given ranger level.</summary>
    public List<string> GetAllFeatsUpToLevel(int rangerLevel)
    {
        var feats = new List<string>();
        if (!HasStyleSelected) return feats;

        int[] levels = { 2, 6, 11 };
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] <= rangerLevel)
            {
                string feat = GetFeatForLevel(levels[i]);
                if (feat != null) feats.Add(feat);
            }
        }
        return feats;
    }

    /// <summary>Get a display summary of the combat style.</summary>
    public string GetSummary()
    {
        if (!HasStyleSelected) return "None selected";
        string styleName = Style == RangerCombatStyle.Archery ? "Archery" : "Two-Weapon Fighting";
        string feats = GrantedFeats.Count > 0 ? string.Join(", ", GrantedFeats) : "none yet";
        return $"{styleName} ({feats})";
    }
}
