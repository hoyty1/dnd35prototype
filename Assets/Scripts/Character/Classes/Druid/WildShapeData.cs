using UnityEngine;

/// <summary>
/// Tracks wild shape transformation state for a Druid character (D&D 3.5e PHB p.37).
/// Manages uses per day, duration, current form, and stat modifications.
/// Wild Shape is gained at level 5 and improves as the druid levels.
/// </summary>
public class WildShapeData
{
    // ── Current state ──
    public bool IsWildShaped { get; private set; }
    public WildShapeForm CurrentForm { get; private set; }
    public int UsesRemaining { get; private set; }
    public int RoundsRemaining { get; private set; }

    // ── Saved original stats (restored on revert) ──
    public int OriginalSTR { get; private set; }
    public int OriginalDEX { get; private set; }
    public int OriginalCON { get; private set; }
    public int OriginalNaturalArmor { get; private set; }
    public int OriginalSpeed { get; private set; }

    // ── Reference ──
    private int _druidLevel;
    private bool _hasNaturalSpellFeat;

    /// <summary>
    /// Initialize wild shape data for a druid of the given level.
    /// </summary>
    public void Initialize(int druidLevel, bool hasNaturalSpellFeat = false)
    {
        _druidLevel = druidLevel;
        _hasNaturalSpellFeat = hasNaturalSpellFeat;
        UsesRemaining = GetUsesPerDay(druidLevel);
        IsWildShaped = false;
        CurrentForm = null;
        RoundsRemaining = 0;
    }

    /// <summary>
    /// Wild Shape uses per day by druid level (PHB p.37).
    /// Gained at level 5 (1/day), +1 per 2 levels thereafter. Max 8 at level 19+.
    /// Level 5: 1, Level 6: 1, Level 7: 2, Level 8: 2, Level 9: 3, ... Level 18: 7 (at-will for elemental at 20)
    /// Actually: 1 at 5, then +1 at 6, 7, 8... = 1 + (level - 5) / 2 rounded down.
    /// PHB: 1/day at 5th, 2/day at 6th, 3/day at 7th... so it's 1 + (level - 5).
    /// Wait — PHB says "she can use this ability more often as she advances in level"
    /// Table 3-8: Wild Shape 1/day at 5, 2/day at 6, 3/day at 7, 4/day at 8,
    /// but that seems too generous. Let me use the standard: 1 at 5, +1 every 3 levels.
    /// Actually per PHB Table 3-8: 1/day(5), 2/day(6), 3/day(7), 4/day(10), 5/day(14), 6/day(18).
    /// The correct progression: 1/day at L5, 2/day at L6, 3/day at L7, then +1 at L10, L14, L18.
    /// </summary>
    public static int GetUsesPerDay(int druidLevel)
    {
        if (druidLevel < 5) return 0;
        // PHB Table 3-8 progression
        int uses = 1;
        if (druidLevel >= 6) uses = 2;
        if (druidLevel >= 7) uses = 3;
        if (druidLevel >= 10) uses = 4;
        if (druidLevel >= 14) uses = 5;
        if (druidLevel >= 18) uses = 6;
        return uses;
    }

    /// <summary>
    /// Duration of wild shape in hours (1 hour per druid level, PHB p.37).
    /// </summary>
    public static int GetDurationHours(int druidLevel)
    {
        return druidLevel < 5 ? 0 : druidLevel;
    }

    /// <summary>
    /// Duration in rounds (10 rounds per minute, 60 minutes per hour).
    /// </summary>
    public static int GetDurationRounds(int druidLevel)
    {
        return GetDurationHours(druidLevel) * 600; // 10 rounds/min × 60 min/hr
    }

    /// <summary>
    /// Whether the druid can currently transform (has uses, is not already shaped, form is available).
    /// </summary>
    public bool CanTransform(WildShapeForm form)
    {
        if (IsWildShaped) return false;
        if (UsesRemaining <= 0) return false;
        if (form == null) return false;
        return WildShapeFormDatabase.IsFormAvailable(form, _druidLevel);
    }

    /// <summary>
    /// Transform into the specified form. Returns true if successful.
    /// Caller should apply stat changes to CharacterStats after this call.
    /// </summary>
    public bool TransformInto(WildShapeForm form, int currentSTR, int currentDEX, int currentCON, int currentNaturalArmor, int currentSpeed)
    {
        if (!CanTransform(form)) return false;

        // Save original physical stats
        OriginalSTR = currentSTR;
        OriginalDEX = currentDEX;
        OriginalCON = currentCON;
        OriginalNaturalArmor = currentNaturalArmor;
        OriginalSpeed = currentSpeed;

        // Apply transformation
        IsWildShaped = true;
        CurrentForm = form;
        UsesRemaining--;
        RoundsRemaining = GetDurationRounds(_druidLevel);

        return true;
    }

    /// <summary>
    /// Get the STR score while in current wild shape form.
    /// </summary>
    public int GetWildShapeSTR() => IsWildShaped && CurrentForm != null ? CurrentForm.STR : 0;

    /// <summary>
    /// Get the DEX score while in current wild shape form.
    /// </summary>
    public int GetWildShapeDEX() => IsWildShaped && CurrentForm != null ? CurrentForm.DEX : 0;

    /// <summary>
    /// Get the CON score while in current wild shape form.
    /// </summary>
    public int GetWildShapeCON() => IsWildShaped && CurrentForm != null ? CurrentForm.CON : 0;

    /// <summary>
    /// Get the natural armor bonus while in current wild shape form.
    /// </summary>
    public int GetWildShapeNaturalArmor() => IsWildShaped && CurrentForm != null ? CurrentForm.NaturalArmor : 0;

    /// <summary>
    /// Get the speed while in current wild shape form.
    /// </summary>
    public int GetWildShapeSpeed() => IsWildShaped && CurrentForm != null ? CurrentForm.Speed : 0;

    /// <summary>
    /// Revert to normal form. Can be done as a free action (PHB p.37).
    /// Returns true if was wild shaped and successfully reverted.
    /// </summary>
    public bool RevertToNormal()
    {
        if (!IsWildShaped) return false;

        IsWildShaped = false;
        CurrentForm = null;
        RoundsRemaining = 0;
        // Caller should restore original physical stats

        return true;
    }

    /// <summary>
    /// Tick one round of wild shape duration. Auto-reverts when duration expires.
    /// Returns true if still wild shaped after tick.
    /// </summary>
    public bool TickRound()
    {
        if (!IsWildShaped) return false;

        RoundsRemaining--;
        if (RoundsRemaining <= 0)
        {
            RevertToNormal();
            return false;
        }
        return true;
    }

    /// <summary>
    /// Whether the druid can cast spells while wild shaped (requires Natural Spell feat, PHB p.97).
    /// </summary>
    public bool CanCastInWildShape => _hasNaturalSpellFeat;

    /// <summary>
    /// Set whether the druid has the Natural Spell feat.
    /// </summary>
    public void SetNaturalSpellFeat(bool hasFeat)
    {
        _hasNaturalSpellFeat = hasFeat;
    }

    /// <summary>
    /// Get a summary string of current wild shape state.
    /// </summary>
    public string GetStatusSummary()
    {
        if (!IsWildShaped)
            return $"Normal form. Uses remaining: {UsesRemaining}/{GetUsesPerDay(_druidLevel)}";

        int minutesLeft = RoundsRemaining / 10;
        return $"Wild Shaped: {CurrentForm.Name} ({CurrentForm.Size} {CurrentForm.FormType}). " +
               $"STR {CurrentForm.STR} DEX {CurrentForm.DEX} CON {CurrentForm.CON}. " +
               $"~{minutesLeft} min remaining. Uses left: {UsesRemaining}/{GetUsesPerDay(_druidLevel)}";
    }
}
