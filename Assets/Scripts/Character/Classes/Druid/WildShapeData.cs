using UnityEngine;

/// <summary>
/// The wild shape tables (uses per day and duration by druid level, PHB p.37), which WildShapeRules reads. The instance
/// members (a form from WildShapeFormDatabase, its own use counter) are an older model that only tests use; the rules
/// keep their state in CharacterStats.ActiveWildShape and take their forms from the bestiary (WildShapeRules; CHR-020,
/// CHR-048).
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
    /// Wild Shape uses per day by druid level (PHB Table 3-8, p.36-37): 1 at 5th, 2 at 6th, 3 at 7th, 4 at 10th, 5 at
    /// 14th, 6 at 18th (the elemental uses from 16th are separate and not modelled). WildShapeRules reads it.
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
    /// Revert to normal form (legacy test model; changing back is a standard action, PHB p.37: WildShapeRules.Revert).
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
