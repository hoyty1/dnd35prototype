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
/// Which kind of bar refused an action (batch 13 step 3): the action catalog's coarse reason class, for the legal set
/// (batch 13 step 6) and the restricted menu (step 20). The reason string stays the probe's own, unchanged.
/// <see cref="Legality.Refused(string)"/> gives <see cref="Other"/>; an allowed answer has <see cref="None"/>.
/// </summary>
public enum LegalityBar
{
    /// <summary>Nothing bars the action (an allowed answer).</summary>
    None,
    /// <summary>The action economy: the action is spent or not available this turn.</summary>
    Action,
    /// <summary>A condition of the actor (prone, grappling, entangled, nauseated, fatigued).</summary>
    Condition,
    /// <summary>A compulsion decides the turn (frightened, confused, suggested; batch 12 step 7).</summary>
    Compulsion,
    /// <summary>The target: none, not a foe, gone, or not one the action can take.</summary>
    Target,
    /// <summary>The target is beyond the attack's reach.</summary>
    Reach,
    /// <summary>The target or point is beyond the action's range.</summary>
    Range,
    /// <summary>No ammunition, or the crossbow is not loaded.</summary>
    Ammunition,
    /// <summary>A spent resource: a spell slot, a daily use, a charge.</summary>
    Resource,
    /// <summary>The gear in hand or worn: no weapon, a wrong weapon, no free hand.</summary>
    Equipment,
    /// <summary>The ground: no path, no line of effect, an occupied square.</summary>
    Terrain,
    /// <summary>A ward or barrier: Sanctuary, Hide from Undead, a Resilient Sphere.</summary>
    Ward,
    /// <summary>A charm forbids the action against its target (batch 13 step 20).</summary>
    Charm,
    /// <summary>Any other bar; every refusal built before batch 13 step 3.</summary>
    Other
}

/// <summary>
/// The answer of a pure legality probe (no dice, no log): may the action go ahead, why not, and what is still unrolled.
/// Built by <see cref="AttackLegality.CanDirectlyAttack"/>; the action catalog's <c>IActionExecutor.CanStart</c> and
/// <c>CanExecute</c> return it (batch 13 step 3), with the <see cref="Bar"/> that refused.
/// </summary>
public readonly struct Legality
{
    public readonly bool Ok;
    public readonly string Reason;
    public readonly LegalityRisk Risk;

    /// <summary>The kind of bar that refused (<see cref="LegalityBar.None"/> when allowed; <see cref="LegalityBar.Other"/> when not given).</summary>
    public readonly LegalityBar Bar;

    public Legality(bool ok, string reason, LegalityRisk risk = LegalityRisk.None)
        : this(ok, reason, risk, LegalityBar.Other)
    {
    }

    public Legality(bool ok, string reason, LegalityRisk risk, LegalityBar bar)
    {
        Ok = ok;
        Reason = reason ?? string.Empty;
        Risk = risk;
        Bar = ok ? LegalityBar.None : (bar == LegalityBar.None ? LegalityBar.Other : bar);
    }

    public static Legality Allowed(LegalityRisk risk = LegalityRisk.None) => new Legality(true, string.Empty, risk);

    /// <summary>A refusal with <see cref="LegalityBar.Other"/> (every refusal written before the catalog).</summary>
    public static Legality Refused(string reason) => new Legality(false, reason);

    /// <summary>A refusal with its kind of bar (batch 13 step 3).</summary>
    public static Legality Refused(LegalityBar bar, string reason) => new Legality(false, reason, LegalityRisk.None, bar);

    public override string ToString() => Ok ? (Risk == LegalityRisk.None ? "ok" : "ok(" + Risk + ")") : "refused(" + Reason + ")";
}
