/// <summary>
/// What a legality probe found that it could not settle without rolling: the shape the action catalog's
/// <c>CanExecute</c> returns (docs/systems/tactics/README.md 1.4) plus <see cref="Risk"/>. Batch 12 step 12.
/// </summary>
public enum LegalityRisk
{
    /// <summary>Nothing unrolled stands in the way.</summary>
    None,

    /// <summary>
    /// The target is warded (Sanctuary, Hide from Undead) and this attacker has not yet saved against that casting: the
    /// attempt is legal, but its commit rolls the Will save and may lose the attack (PHB p.274; WardSaveLedger).
    /// </summary>
    WardSaveUnrolled
}

/// <summary>
/// The answer of a pure legality probe (no dice, no log): may the action go ahead, why not, and what is still unrolled.
/// Built by <see cref="AttackLegality.CanDirectlyAttack"/>; batch 13 reuses it for the action catalog.
/// </summary>
public readonly struct Legality
{
    public readonly bool Ok;
    public readonly string Reason;
    public readonly LegalityRisk Risk;

    public Legality(bool ok, string reason, LegalityRisk risk = LegalityRisk.None)
    {
        Ok = ok;
        Reason = reason ?? string.Empty;
        Risk = risk;
    }

    public static Legality Allowed(LegalityRisk risk = LegalityRisk.None) => new Legality(true, string.Empty, risk);

    public static Legality Refused(string reason) => new Legality(false, reason);

    public override string ToString() => Ok ? (Risk == LegalityRisk.None ? "ok" : "ok(" + Risk + ")") : "refused(" + Reason + ")";
}
