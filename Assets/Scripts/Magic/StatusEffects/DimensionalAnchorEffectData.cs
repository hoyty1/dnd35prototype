using UnityEngine;

/// <summary>
/// Runtime payload for Dimensional Anchor (PHB p.221).
/// Abjuration. Cleric 4, Sorcerer/Wizard 4. V, S.
/// Range: Medium (100 ft + 10 ft/level). Duration: 1 min/level.
/// Ranged touch attack (ray). No save. SR: Yes.
///
/// Bars all extradimensional travel for the target, including:
/// teleport, dimension door, plane shift, etherealness, blink,
/// astral projection, gate, maze, shadow walk, etc.
/// </summary>
[System.Serializable]
public class DimensionalAnchorEffectData
{
    /// <summary>Which categories of teleportation are blocked. Dimensional Anchor blocks ALL.</summary>
    public TeleportationType BlockedTypes = TeleportationType.All;

    /// <summary>Remaining duration in rounds (synced with ActiveSpellEffect).</summary>
    public int DurationRemainingRounds;

    /// <summary>Name of the caster who applied this effect.</summary>
    public string CasterName;

    /// <summary>Caster level used for the spell (affects duration).</summary>
    public int CasterLevel;

    /// <summary>Number of teleportation attempts blocked so far (for UI tracking).</summary>
    public int AttemptsBlocked;

    /// <summary>
    /// Factory method to create a new Dimensional Anchor effect.
    /// </summary>
    /// <param name="casterLevel">Effective caster level.</param>
    /// <param name="casterName">Name of the caster.</param>
    /// <param name="durationRounds">Pre-calculated duration in rounds (1 min/level = 10 rounds/level).</param>
    public static DimensionalAnchorEffectData Create(int casterLevel, string casterName, int durationRounds)
    {
        return new DimensionalAnchorEffectData
        {
            BlockedTypes = TeleportationType.All,
            DurationRemainingRounds = durationRounds,
            CasterName = casterName ?? "Unknown",
            CasterLevel = casterLevel,
            AttemptsBlocked = 0
        };
    }
}
