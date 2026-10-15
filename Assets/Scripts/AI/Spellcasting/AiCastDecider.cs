using DND35e.Identifiers;

/// <summary>
/// The NPC decider (batch 11, cast_core_batch11.md): it answers a cast's choices inline, so an NPC cast runs through the
/// cast core synchronously (GameManager.RunCastSync, from TryNPCPerformSpellCast since batch 11 step 8). For now it
/// gives the defaults the NPC cast path has always used, so the rules code reads the request and never asks who
/// controls the caster. Real AI choices are batch 11 step 22 (SPL-112, SPL-113).
/// </summary>
public sealed class AiCastDecider : ICastDecider
{
    public static readonly AiCastDecider Instance = new AiCastDecider();

    private AiCastDecider() { }

    public bool IsSynchronous => true;

    /// <summary>
    /// Today's NPC defaults (<see cref="FillDefaultChoices"/>): fire for Resist Energy and Protection from Energy, the
    /// warm Fire Shield, the caster's own race for Disguise Self (no race chosen) and no weapon for Magic Weapon, Keen
    /// Edge and Greater Magic Weapon, so those still fizzle for an NPC (SPL-113). Never suspends, never refuses.
    /// </summary>
    public CastChoiceFill FillChoices(GameManager gm, CastContext ctx)
    {
        FillDefaultChoices(ctx != null ? ctx.Request : null);
        return CastChoiceFill.Filled;
    }

    /// <summary>
    /// Today's NPC decision and rolls for a threatened cast (AISpellcastingStrategist.ShouldCastDefensively, then
    /// ResolveThreatenedSpellcast, through GameManager.ResolveNPCSpellcastProvocation), answered at once: the spell
    /// proceeds or is lost; the AI never cancels.
    /// </summary>
    public void ChooseProvocation(GameManager gm, CastContext ctx, System.Action<CastProvocationAnswer> answer)
    {
        bool proceeds = gm.AiResolveCastProvocation(ctx);
        answer?.Invoke(proceeds ? CastProvocationAnswer.Proceeds : CastProvocationAnswer.Lost);
    }

    /// <summary>Nothing to do before the combat-end check.</summary>
    public void BeforeCombatEndCheck(GameManager gm, CastContext ctx) { }

    /// <summary>
    /// The NPC path's follow-up of a cast that counts as cast (GameManager.AiOnCastFinished): the result log becomes the
    /// last combat log and the stat panels refresh (which also refreshes the Flanked conditions), as the old NPC
    /// executor did before it returned. The NPC turn then goes on by itself.
    /// </summary>
    public void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome) => gm.AiOnCastFinished(ctx, outcome);

    /// <summary>
    /// Fills the choices the NPC cast path has always made for the caster, where the request has none yet: fire for
    /// Resist Energy (PHB p.272) and Protection from Energy (PHB p.266), and the warm form of Fire Shield (PHB p.230).
    /// The values are today's; the AI does not weigh the options yet (SPL-112).
    /// </summary>
    internal void FillDefaultChoices(SpellCastRequest request)
    {
        if (request == null || request.Spell == null || request.Choices == null)
            return;

        string id = request.Spell.SpellId;
        if ((id == SpellNames.RESIST_ENERGY || id == SpellNames.PROTECTION_FROM_ENERGY) && !request.Choices.Energy.HasValue)
            request.Choices.Energy = ResistEnergyType.Fire;
        else if (id == SpellNames.FIRE_SHIELD && !request.Choices.FireShieldWarm.HasValue)
            request.Choices.FireShieldWarm = true;
    }
}
