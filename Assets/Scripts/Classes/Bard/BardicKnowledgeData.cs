// ============================================================================
// D&D 3.5e Bardic Knowledge (PHB p.28)
// A Bard can make a special Bardic Knowledge check to recall obscure lore.
// Check = 1d20 + Bard level + Int modifier.
// ============================================================================

using System;
using UnityEngine;

/// <summary>
/// Bardic Knowledge: A special lore check that represents the Bard's
/// extensive learning from songs, tales, and wandering.
/// Can substitute for any Knowledge check.
///
/// D&D 3.5e PHB p.28:
///   Check: 1d20 + Bard level + Int modifier
///   DC 10: Common, well-known lore
///   DC 20: Uncommon lore
///   DC 25: Obscure lore
///   DC 30: Extremely obscure, known only to few
/// </summary>
[Serializable]
public class BardicKnowledgeData
{
    [SerializeField] private int _bardLevel;
    [SerializeField] private int _intModifier;

    /// <summary>Current bard level.</summary>
    public int BardLevel => _bardLevel;

    /// <summary>INT modifier for the check.</summary>
    public int IntModifier => _intModifier;

    /// <summary>Total modifier for Bardic Knowledge checks (level + Int).</summary>
    public int TotalModifier => _bardLevel + _intModifier;

    /// <summary>Initialize or update when level/stats change.</summary>
    public void Initialize(int bardLevel, int intModifier)
    {
        _bardLevel = bardLevel;
        _intModifier = intModifier;
    }

    /// <summary>
    /// Roll a Bardic Knowledge check.
    /// Result = 1d20 + Bard level + Int modifier.
    /// </summary>
    public int RollBardicKnowledge()
    {
        int roll = DiceRoller.D20();
        int total = roll + TotalModifier;
        Debug.Log($"[BardicKnowledge] Check: {roll} + {TotalModifier} (L{_bardLevel} + INT {_intModifier}) = {total}");
        return total;
    }

    /// <summary>
    /// Get the DC result category for a given check total.
    /// </summary>
    public static string GetResultCategory(int checkTotal)
    {
        if (checkTotal >= 30) return "Extremely Obscure — known only to a select few";
        if (checkTotal >= 25) return "Obscure — known by few, hard to find in books";
        if (checkTotal >= 20) return "Uncommon — not widely known";
        if (checkTotal >= 10) return "Common — well-known facts and legends";
        return "No useful knowledge recalled";
    }

    /// <summary>
    /// Determine minimum check total needed for a given DC tier.
    /// </summary>
    public static int GetDCForTier(string tier)
    {
        switch (tier.ToLowerInvariant())
        {
            case "common": return 10;
            case "uncommon": return 20;
            case "obscure": return 25;
            case "extremely obscure": return 30;
            default: return 10;
        }
    }
}
