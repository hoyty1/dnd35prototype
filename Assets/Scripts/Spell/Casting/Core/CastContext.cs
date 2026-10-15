/// <summary>
/// The state of one open cast (batch 11, cast_core_batch11.md): its <see cref="SpellCastRequest"/> and the fields the
/// staged cast core (step 7 on) fills as the cast runs. GameManager keeps open contexts on a stack (BeginCast and
/// EndCast, Spell/Resolution/GameManager.CastContext.cs): a synchronous AI cast nests on top of a Ui cast that waits for
/// the player, and a pop that does not match the top is a scenario-harness violation.
/// </summary>
public sealed class CastContext
{
    public readonly SpellCastRequest Request;

    /// <summary>The cast's SpellCaster result, once resolved (filled by the cast core, step 7 on).</summary>
    public SpellResult Result;

    /// <summary>Whether a save negated the effect (filled by the cast core, step 7 on).</summary>
    public bool EffectNegatedBySave;

    /// <summary>The caster level and save DC the cast uses (filled by the cast core, steps 7 and 14).</summary>
    public int CasterLevel;
    public int SaveDC;

    /// <summary>The spontaneous and quickened log prefixes, built once (filled by the cast core, step 7 on).</summary>
    public string LogPrefix;

    /// <summary>The actions and spells per day before the spend, for a cancel to give back (moves here with the cast core).</summary>
    internal CastResourceSnapshot Snapshot;

    /// <summary>Set when the context left the cast stack (EndCast, or a combat reset).</summary>
    public bool Ended { get; internal set; }

    public CastContext(SpellCastRequest request)
    {
        Request = request ?? new SpellCastRequest();
    }
}
