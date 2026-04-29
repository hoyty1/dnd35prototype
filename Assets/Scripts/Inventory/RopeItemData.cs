using System;

/// <summary>
/// Rope item metadata used by Animate Rope and escape mechanics.
/// </summary>
[Serializable]
public class RopeItemData : ItemData
{
    /// <summary>
    /// Strength check DC required to break this rope.
    /// </summary>
    public int BreakDC;

    /// <summary>
    /// Rope length in feet (for UI/tooltip and spell flavor handling).
    /// </summary>
    public int LengthFeet = 50;
}