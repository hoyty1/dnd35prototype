using UnityEngine;

/// <summary>
/// D&D 3.5e negative level helper.
/// Tracks application/removal through the Energy Drained condition stack.
/// </summary>
public static class NegativeLevelSystem
{
    public static int GetNegativeLevels(CharacterController character)
    {
        return character != null ? character.NegativeLevelCount : 0;
    }

    public static int ApplyNegativeLevels(CharacterController target, int amount, string sourceName)
    {
        if (target == null || target.Stats == null || amount <= 0)
            return GetNegativeLevels(target);

        int before = target.NegativeLevelCount;
        int after = target.ApplyNegativeLevels(amount, sourceName);

        Debug.Log($"[NegativeLevelSystem] {target.Stats.CharacterName}: {before} -> {after} negative level(s) from {sourceName}.");
        return after;
    }

    public static int RemoveNegativeLevels(CharacterController target, int amount, string sourceName)
    {
        if (target == null || target.Stats == null || amount <= 0)
            return 0;

        int removed = target.RemoveNegativeLevels(amount);
        Debug.Log($"[NegativeLevelSystem] {target.Stats.CharacterName}: removed {removed} negative level(s) via {sourceName}.");
        return removed;
    }

    public static bool IsDeadFromNegativeLevels(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return false;

        return target.Stats.EnforceNegativeLevelDeathThreshold();
    }
}
