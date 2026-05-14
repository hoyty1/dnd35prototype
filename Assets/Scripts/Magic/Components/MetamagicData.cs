using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ============================================================================
// D&D 3.5 Metamagic System - Data Structures
// ============================================================================

/// <summary>
/// Identifies specific metamagic feats for quick lookup.
/// </summary>
public enum MetamagicFeatId
{
    None,
    EmpowerSpell,       // +2 spell level: numeric effects ×1.5
    EnlargeSpell,       // +1 spell level: double range
    ExtendSpell,        // +1 spell level: double duration
    HeightenSpell,      // +variable spell level: increase save DC
    MaximizeSpell,      // +3 spell level: maximize dice
    QuickenSpell,       // +4 spell level: cast as free action
    SilentSpell,        // +1 spell level: no verbal components
    StillSpell,         // +1 spell level: no somatic components
    WidenSpell          // +3 spell level: double area
}

/// <summary>
/// Tracks which metamagic feats are applied to a single spell cast.
/// Created fresh for each spell casting attempt.
/// </summary>
[System.Serializable]
public class MetamagicData
{
    /// <summary>Set of metamagic feats applied to this spell.</summary>
    public HashSet<MetamagicFeatId> AppliedMetamagic = new HashSet<MetamagicFeatId>();

    /// <summary>For Heighten Spell: the target spell level to heighten to.</summary>
    public int HeightenToLevel = -1;

    /// <summary>Whether any metamagic is applied.</summary>
    public bool HasAnyMetamagic => AppliedMetamagic.Count > 0;

    /// <summary>Check if a specific metamagic is applied.</summary>
    public bool Has(MetamagicFeatId feat) => AppliedMetamagic.Contains(feat);

    /// <summary>Toggle a metamagic feat on/off.</summary>
    public void Toggle(MetamagicFeatId feat)
    {
        if (AppliedMetamagic.Contains(feat))
            AppliedMetamagic.Remove(feat);
        else
            AppliedMetamagic.Add(feat);
    }

    /// <summary>
    /// Calculate the total spell level adjustment from all applied metamagic.
    /// </summary>
    public int GetTotalLevelAdjustment(int baseSpellLevel)
    {
        int adj = 0;
        foreach (var mm in AppliedMetamagic)
        {
            adj += GetLevelAdjustment(mm, baseSpellLevel);
        }
        return adj;
    }

    /// <summary>
    /// Get the level adjustment for a specific metamagic feat.
    /// </summary>
    public int GetLevelAdjustment(MetamagicFeatId feat, int baseSpellLevel)
    {
        switch (feat)
        {
            case MetamagicFeatId.EmpowerSpell:  return 2;
            case MetamagicFeatId.EnlargeSpell:  return 1;
            case MetamagicFeatId.ExtendSpell:   return 1;
            case MetamagicFeatId.HeightenSpell:
                // Heighten raises spell to chosen level
                if (HeightenToLevel > baseSpellLevel)
                    return HeightenToLevel - baseSpellLevel;
                return 0;
            case MetamagicFeatId.MaximizeSpell:  return 3;
            case MetamagicFeatId.QuickenSpell:   return 4;
            case MetamagicFeatId.SilentSpell:    return 1;
            case MetamagicFeatId.StillSpell:     return 1;
            case MetamagicFeatId.WidenSpell:     return 3;
            default: return 0;
        }
    }

    /// <summary>
    /// Get the effective spell level (base + metamagic adjustments).
    /// This is the slot level consumed.
    /// </summary>
    public int GetEffectiveSpellLevel(int baseSpellLevel)
    {
        return baseSpellLevel + GetTotalLevelAdjustment(baseSpellLevel);
    }

    /// <summary>
    /// Check if Empower can be applied to a spell (spell must have numeric effects).
    /// </summary>
    public static bool CanEmpower(SpellData spell)
    {
        return spell.EffectType == SpellEffectType.Damage || spell.EffectType == SpellEffectType.Healing;
    }

    /// <summary>
    /// Check if Maximize can be applied to a spell (spell must have dice).
    /// </summary>
    public static bool CanMaximize(SpellData spell)
    {
        return spell.EffectType == SpellEffectType.Damage || spell.EffectType == SpellEffectType.Healing;
    }

    /// <summary>
    /// Check if Enlarge can be applied (spell must have range > 0).
    /// </summary>
    public static bool CanEnlarge(SpellData spell)
    {
        return spell.RangeSquares > 0; // Can't enlarge touch or self spells
    }

    /// <summary>
    /// Check if Extend can be applied (spell must have a duration).
    /// </summary>
    public static bool CanExtend(SpellData spell)
    {
        return spell.BuffDurationRounds != 0; // Has some duration (rounds or hours/level)
    }

    /// <summary>
    /// Check if Widen can be applied (spell must have an area).
    /// </summary>
    public static bool CanWiden(SpellData spell)
    {
        return spell.AreaRadius > 0 || spell.TargetType == SpellTargetType.Area;
    }

    /// <summary>
    /// Check if a specific metamagic feat is applicable to a given spell.
    /// </summary>
    public static bool IsApplicable(MetamagicFeatId feat, SpellData spell)
    {
        switch (feat)
        {
            case MetamagicFeatId.EmpowerSpell:  return CanEmpower(spell);
            case MetamagicFeatId.MaximizeSpell:  return CanMaximize(spell);
            case MetamagicFeatId.EnlargeSpell:   return CanEnlarge(spell);
            case MetamagicFeatId.ExtendSpell:    return CanExtend(spell);
            case MetamagicFeatId.WidenSpell:     return CanWiden(spell);
            // These metamagic feats can be applied to any spell:
            case MetamagicFeatId.HeightenSpell:  return true;
            case MetamagicFeatId.QuickenSpell:   return true;
            case MetamagicFeatId.SilentSpell:    return true;
            case MetamagicFeatId.StillSpell:     return true;
            default: return false;
        }
    }

    /// <summary>
    /// Get display name for a metamagic feat.
    /// </summary>
    public static string GetDisplayName(MetamagicFeatId feat)
    {
        switch (feat)
        {
            case MetamagicFeatId.EmpowerSpell:  return "Empower Spell";
            case MetamagicFeatId.EnlargeSpell:  return "Enlarge Spell";
            case MetamagicFeatId.ExtendSpell:   return "Extend Spell";
            case MetamagicFeatId.HeightenSpell: return "Heighten Spell";
            case MetamagicFeatId.MaximizeSpell: return "Maximize Spell";
            case MetamagicFeatId.QuickenSpell:  return "Quicken Spell";
            case MetamagicFeatId.SilentSpell:   return "Silent Spell";
            case MetamagicFeatId.StillSpell:    return "Still Spell";
            case MetamagicFeatId.WidenSpell:    return "Widen Spell";
            default: return "Unknown";
        }
    }

    /// <summary>
    /// Get short effect description for a metamagic feat.
    /// </summary>
    public static string GetShortEffect(MetamagicFeatId feat)
    {
        switch (feat)
        {
            case MetamagicFeatId.EmpowerSpell:  return "×1.5 damage/healing (+2 lvl)";
            case MetamagicFeatId.EnlargeSpell:  return "×2 range (+1 lvl)";
            case MetamagicFeatId.ExtendSpell:   return "×2 duration (+1 lvl)";
            case MetamagicFeatId.HeightenSpell: return "Increase DC (+var lvl)";
            case MetamagicFeatId.MaximizeSpell: return "Max dice (+3 lvl)";
            case MetamagicFeatId.QuickenSpell:  return "Free action (+4 lvl)";
            case MetamagicFeatId.SilentSpell:   return "No verbal (+1 lvl)";
            case MetamagicFeatId.StillSpell:    return "No somatic (+1 lvl)";
            case MetamagicFeatId.WidenSpell:    return "×2 area (+3 lvl)";
            default: return "";
        }
    }

    /// <summary>
    /// Get a summary string of all applied metamagic.
    /// </summary>
    public string GetSummary(int baseSpellLevel)
    {
        if (!HasAnyMetamagic) return "";
        var sb = new StringBuilder();
        sb.Append("Metamagic: ");
        var parts = new List<string>();
        foreach (var mm in AppliedMetamagic)
        {
            parts.Add(GetDisplayName(mm));
        }
        sb.Append(string.Join(", ", parts));
        sb.Append($" (Lv{baseSpellLevel} → Lv{GetEffectiveSpellLevel(baseSpellLevel)} slot)");
        return sb.ToString();
    }

    /// <summary>
    /// Get all defined metamagic feat IDs.
    /// </summary>
    public static MetamagicFeatId[] AllMetamagicFeats = new MetamagicFeatId[]
    {
        MetamagicFeatId.EmpowerSpell,
        MetamagicFeatId.EnlargeSpell,
        MetamagicFeatId.ExtendSpell,
        MetamagicFeatId.HeightenSpell,
        MetamagicFeatId.MaximizeSpell,
        MetamagicFeatId.QuickenSpell,
        MetamagicFeatId.SilentSpell,
        MetamagicFeatId.StillSpell,
        MetamagicFeatId.WidenSpell
    };

    /// <summary>
    /// Map from feat name (as stored in CharacterStats.Feats) to MetamagicFeatId.
    /// </summary>
    public static MetamagicFeatId GetIdFromFeatName(string featName)
    {
        switch (featName)
        {
            case "Empower Spell":   return MetamagicFeatId.EmpowerSpell;
            case "Enlarge Spell":   return MetamagicFeatId.EnlargeSpell;
            case "Extend Spell":    return MetamagicFeatId.ExtendSpell;
            case "Heighten Spell":  return MetamagicFeatId.HeightenSpell;
            case "Maximize Spell":  return MetamagicFeatId.MaximizeSpell;
            case "Quicken Spell":   return MetamagicFeatId.QuickenSpell;
            case "Silent Spell":    return MetamagicFeatId.SilentSpell;
            case "Still Spell":     return MetamagicFeatId.StillSpell;
            case "Widen Spell":     return MetamagicFeatId.WidenSpell;
            default: return MetamagicFeatId.None;
        }
    }

    /// <summary>
    /// Get the feat name string for a metamagic feat ID.
    /// </summary>
    public static string GetFeatName(MetamagicFeatId id)
    {
        return GetDisplayName(id); // They're the same
    }
}
