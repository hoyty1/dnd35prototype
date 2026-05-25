using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assigns equipment to NPCs based on template data (D&D 3.5e DMG Chapter 4).
/// Converts template equipment lists to actual game items and calculates wealth totals.
/// </summary>
public static class EquipmentAssigner
{
    /// <summary>
    /// Calculate total equipment value from a template's equipment list.
    /// </summary>
    public static int CalculateEquipmentValue(List<EquipmentItem> equipment)
    {
        if (equipment == null) return 0;
        int total = 0;
        foreach (var item in equipment)
            total += item.ValueGP;
        return total;
    }

    /// <summary>
    /// Check if total equipment value is within expected wealth for level.
    /// Returns the expected wealth for the given level per DMG Table 5-1.
    /// </summary>
    public static int GetExpectedWealthByLevel(int level)
    {
        // DMG Table 5-1: NPC Gear Value
        switch (level)
        {
            case 1: return 300;
            case 2: return 900;
            case 3: return 2700;
            case 4: return 5400;
            case 5: return 9000;
            case 6: return 13000;
            case 7: return 19000;
            case 8: return 27000;
            case 9: return 36000;
            case 10: return 49000;
            case 11: return 66000;
            case 12: return 88000;
            case 13: return 110000;
            case 14: return 150000;
            case 15: return 200000;
            case 16: return 260000;
            case 17: return 340000;
            case 18: return 440000;
            case 19: return 580000;
            case 20: return 760000;
            default:
                if (level < 1) return 0;
                return 760000; // Cap at L20 value
        }
    }

    /// <summary>
    /// Get number of magic items expected for an NPC of this level (rough guide).
    /// </summary>
    public static int GetExpectedMagicItemCount(int level)
    {
        if (level <= 2) return 0;
        if (level <= 5) return 2;
        if (level <= 10) return 4;
        if (level <= 15) return 6;
        return 8;
    }

    /// <summary>
    /// Count magical items in an equipment list.
    /// </summary>
    public static int CountMagicItems(List<EquipmentItem> equipment)
    {
        if (equipment == null) return 0;
        int count = 0;
        foreach (var item in equipment)
            if (item.IsMagical) count++;
        return count;
    }

    /// <summary>
    /// Get a human-readable equipment summary string.
    /// </summary>
    public static string GetEquipmentSummary(NPCTemplate template)
    {
        if (template == null || template.Equipment == null || template.Equipment.Count == 0)
            return "No equipment";

        var sb = new System.Text.StringBuilder();
        foreach (var item in template.Equipment)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(item.ItemName);
            if (item.IsMagical) sb.Append("*");
        }
        sb.Append($" (Total: {template.TotalWealthGP}gp)");
        return sb.ToString();
    }
}
