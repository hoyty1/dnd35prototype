// ============================================================================
// D&D 3.5e Ranger Combat Style (PHB p.48)
// At 2nd level a ranger selects archery or two-weapon combat. He is treated as having Rapid Shot or Two-Weapon
// Fighting (2nd), Manyshot or Improved Two-Weapon Fighting (Improved Combat Style, 6th) and Improved Precise Shot or
// Greater Two-Weapon Fighting (Combat Style Mastery, 11th), without their prerequisites, but only while he wears light
// or no armor. The rules are in RangerRules (CHR-020).
// ============================================================================

using UnityEngine;

/// <summary>Ranger combat style choice (permanent, selected at level 2).</summary>
public enum RangerCombatStyle
{
    None = 0,
    Archery,
    TwoWeaponFighting
}

/// <summary>
/// The ranger's combat style choice. Pure data: the style's feats are never added to <see cref="CharacterStats.Feats"/>;
/// <see cref="CharacterStats.HasFeat"/> treats the ranger as having them while the style applies
/// (<see cref="RangerRules.CombatStyleTreatsAsHavingFeat"/>), for PCs and NPCs alike.
/// </summary>
public class CombatStyleData
{
    public RangerCombatStyle Style { get; private set; } = RangerCombatStyle.None;
    public bool HasStyleSelected => Style != RangerCombatStyle.None;

    /// <summary>A style data object with <paramref name="style"/> already chosen (None leaves it unchosen).</summary>
    public static CombatStyleData Create(RangerCombatStyle style)
    {
        var data = new CombatStyleData();
        if (style != RangerCombatStyle.None)
            data.SelectStyle(style);
        return data;
    }

    /// <summary>Select a combat style. It can be chosen only once (PHB p.48: a choice made at 2nd level).</summary>
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

    /// <summary>The feat the style gives at exactly <paramref name="rangerLevel"/> (2nd, 6th, 11th), or null.</summary>
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

    /// <summary>Whether a given ranger level gives a combat style feat.</summary>
    public static bool IsStyleFeatLevel(int rangerLevel)
    {
        return rangerLevel == 2 || rangerLevel == 6 || rangerLevel == 11;
    }

    /// <summary>Every feat the style gives up to and including <paramref name="rangerLevel"/>.</summary>
    public System.Collections.Generic.List<string> GetAllFeatsUpToLevel(int rangerLevel)
    {
        var feats = new System.Collections.Generic.List<string>();
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

    /// <summary>"Archery", "Two-Weapon Combat" or "None selected".</summary>
    public string GetStyleName()
    {
        switch (Style)
        {
            case RangerCombatStyle.Archery: return "Archery";
            case RangerCombatStyle.TwoWeaponFighting: return "Two-Weapon Combat";
            default: return "None selected";
        }
    }

    /// <summary>A copy of the choice.</summary>
    public CombatStyleData Clone() => Create(Style);
}
