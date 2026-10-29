/// <summary>
/// Adapters from the older legality shapes into <see cref="Legality"/> (batch 13 step 3): a disabled-reason string (the
/// GameManager.Get*DisabledReason family), a bool and reason, and a cast core outcome. The reason strings pass through
/// unchanged; only the <see cref="LegalityBar"/> is added.
/// </summary>
public static class ActionLegality
{
    /// <summary>Empty or null: allowed; otherwise refused with the reason.</summary>
    public static Legality FromDisabledReason(string reason, LegalityBar bar = LegalityBar.Other)
        => string.IsNullOrEmpty(reason) ? Legality.Allowed() : Legality.Refused(bar, reason);

    public static Legality FromCheck(bool ok, string reason, LegalityBar bar = LegalityBar.Other)
        => ok ? Legality.Allowed() : Legality.Refused(bar, string.IsNullOrEmpty(reason) ? "not allowed" : reason);

    /// <summary>
    /// A cast core outcome (GameManager.CheckLegality, RunCastSync): a refusal is refused with its reason (and its detail
    /// when it has one) and the bar its reason names; any other kind is allowed.
    /// </summary>
    public static Legality FromCast(CastOutcome outcome)
    {
        if (outcome == null)
            return Legality.Refused(LegalityBar.Other, "no-cast");
        if (outcome.Kind != CastOutcomeKind.Refused)
            return Legality.Allowed();
        string reason = string.IsNullOrEmpty(outcome.Detail) ? outcome.Reason : outcome.Reason + ": " + outcome.Detail;
        return Legality.Refused(CastBar(outcome.Reason), reason);
    }

    /// <summary>The bar a cast core refusal reason names (the Legality stage's reason tokens).</summary>
    public static LegalityBar CastBar(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return LegalityBar.Other;
        if (reason == "invalid-target" || reason == "choice-refused" || reason == "choices-incomplete")
            return LegalityBar.Target;
        if (reason == "out-of-range" || reason == "origin-out-of-range")
            return LegalityBar.Range;
        if (reason == "origin-no-line-of-effect" || reason == "summon-needs-placement" || reason == "area-needs-aim")
            return LegalityBar.Terrain;
        if (reason == "grapple" || reason == "silenced")
            return LegalityBar.Condition;
        if (reason == CastOutcome.ReasonNoSpellcasting || reason == CastOutcome.ReasonSlotNotSpent)
            return LegalityBar.Resource;
        if (reason.Contains("action"))
            return LegalityBar.Action;
        if (reason.Contains("ward") || reason.Contains("sphere"))
            return LegalityBar.Ward;
        return LegalityBar.Other;
    }
}
