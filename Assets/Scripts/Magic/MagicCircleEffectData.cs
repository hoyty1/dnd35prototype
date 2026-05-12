using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Runtime data for Magic Circle against Evil/Good/Law/Chaos (D&D 3.5e PHB).
/// Tracks the 10-ft radius emanation centered on the touched creature.
/// Standard action version only (not the 10-minute ritual containment version).
///
/// Effects:
///   1. +2 deflection bonus to AC vs creatures of warded alignment
///   2. +2 resistance bonus to saves vs creatures of warded alignment
///   3. Immunity to new possession/mental control from warded alignment
///   4. Suppresses existing mental control while in area
///   5. Blocks bodily contact by summoned/conjured creatures of warded alignment (requires SR check)
///
/// Area: 10-ft radius emanation that moves with the center creature.
/// Does NOT stack with Protection from [Alignment] spells.
/// </summary>
[System.Serializable]
public class MagicCircleEffectData
{
    /// <summary>The alignment component being warded against (Evil, Good, Law, Chaos).</summary>
    public AlignmentProtectionType WardedAlignment = AlignmentProtectionType.None;

    /// <summary>The creature this emanation is centered on (the touched creature).</summary>
    [System.NonSerialized] public CharacterController CenterCreature;

    /// <summary>Caster level for SR checks against summoned creature barrier.</summary>
    public int CasterLevel;

    /// <summary>Remaining duration in rounds.</summary>
    public int RemainingRounds;

    /// <summary>Radius in grid units (10 ft = 2 squares at 5ft/square).</summary>
    public int RadiusSquares = 2;

    /// <summary>Radius in feet for display purposes.</summary>
    public float RadiusFeet = 10f;

    /// <summary>The spell ID that created this effect.</summary>
    public string SourceSpellId;

    /// <summary>Name of the caster for logging.</summary>
    public string CasterName;

    /// <summary>
    /// Check if a creature is within the Magic Circle emanation area.
    /// Uses simplified grid distance (all allies within RadiusSquares of center creature).
    /// </summary>
    public bool IsCreatureInArea(CharacterController creature)
    {
        if (creature == null || CenterCreature == null)
            return false;

        // The center creature is always in its own area
        if (creature == CenterCreature)
            return true;

        // Use grid position distance
        Vector2Int centerPos = CenterCreature.GridPosition;
        Vector2Int creaturePos = creature.GridPosition;
        int dx = Mathf.Abs(centerPos.x - creaturePos.x);
        int dy = Mathf.Abs(centerPos.y - creaturePos.y);

        // Chebyshev distance for grid-based 10-ft radius (2 squares)
        int distance = Mathf.Max(dx, dy);
        return distance <= RadiusSquares;
    }

    /// <summary>
    /// Check if an attacker's alignment matches the warded alignment component.
    /// </summary>
    public bool IsAttackerOfWardedAlignment(Alignment attackerAlignment)
    {
        return AlignmentProtectionRules.Matches(WardedAlignment, attackerAlignment);
    }

    /// <summary>
    /// Get all allies within the emanation area.
    /// </summary>
    public List<CharacterController> GetCreaturesInArea(List<CharacterController> allCharacters)
    {
        var result = new List<CharacterController>();
        if (allCharacters == null || CenterCreature == null)
            return result;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            if (allCharacters[i] != null && !allCharacters[i].IsDead && IsCreatureInArea(allCharacters[i]))
                result.Add(allCharacters[i]);
        }
        return result;
    }

    /// <summary>
    /// Tick the duration. Returns true if the effect is still active.
    /// </summary>
    public bool Tick()
    {
        if (RemainingRounds > 0)
            RemainingRounds--;
        return RemainingRounds > 0;
    }

    /// <summary>
    /// Get the spell name for this Magic Circle variant.
    /// </summary>
    public string GetSpellName()
    {
        switch (WardedAlignment)
        {
            case AlignmentProtectionType.Evil: return "Magic Circle against Evil";
            case AlignmentProtectionType.Good: return "Magic Circle against Good";
            case AlignmentProtectionType.Law: return "Magic Circle against Law";
            case AlignmentProtectionType.Chaos: return "Magic Circle against Chaos";
            default: return "Magic Circle";
        }
    }

    /// <summary>
    /// Get a display string for the remaining duration.
    /// </summary>
    public string GetDurationDisplay()
    {
        if (RemainingRounds <= 0) return "Expired";
        int minutes = RemainingRounds / 10;
        int rounds = RemainingRounds % 10;
        if (minutes > 0 && rounds > 0)
            return $"{minutes} min {rounds} rnd";
        if (minutes > 0)
            return $"{minutes} min";
        return $"{rounds} rnd";
    }
}
