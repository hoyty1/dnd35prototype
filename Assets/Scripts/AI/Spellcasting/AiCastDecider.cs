using DND35e.Identifiers;

/// <summary>
/// The NPC decider (batch 11, cast_core_batch11.md): it answers a cast's choices inline. For now it carries only the
/// defaults the NPC cast path used to set inside the rules code (GameManager.ApplySpellBuff's
/// <c>!caster.IsControllable</c> branches and the Fire Shield handler's default), so the rules code reads the request
/// and no longer asks who controls the caster. Real AI choices are batch 11 step 22 (SPL-112, SPL-113).
/// </summary>
public sealed class AiCastDecider : ICastDecider
{
    public static readonly AiCastDecider Instance = new AiCastDecider();

    private AiCastDecider() { }

    public bool IsSynchronous => true;

    /// <summary>The AI request's choices are filled when it is built (<see cref="FillDefaultChoices"/>).</summary>
    public CastChoiceFill FillChoices(GameManager gm, CastContext ctx) => CastChoiceFill.Filled;

    /// <summary>
    /// Today's NPC decision and rolls for a threatened cast (AISpellcastingStrategist.ShouldCastDefensively, then
    /// ResolveThreatenedSpellcast, through GameManager.ResolveNPCSpellcastProvocation). The NPC cast path does not run
    /// through the cast core until batch 11 step 8, so no cast asks this yet.
    /// </summary>
    public void ChooseProvocation(GameManager gm, CastContext ctx, System.Action<CastProvocationAnswer> answer)
    {
        bool proceeds = gm.AiResolveCastProvocation(ctx);
        answer?.Invoke(proceeds ? CastProvocationAnswer.Proceeds : CastProvocationAnswer.Lost);
    }

    /// <summary>Nothing to do before the combat-end check.</summary>
    public void BeforeCombatEndCheck(GameManager gm, CastContext ctx) { }

    /// <summary>The NPC turn goes on by itself; nothing to do after a cast.</summary>
    public void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome) { }

    /// <summary>
    /// Fills the choices the NPC cast path has always made for the caster: fire for Resist Energy (PHB p.272) and
    /// Protection from Energy (PHB p.266), and the warm form of Fire Shield (PHB p.230). The values are today's; the
    /// AI does not weigh the options yet (SPL-112).
    /// </summary>
    internal void FillDefaultChoices(SpellCastRequest request)
    {
        if (request == null || request.Spell == null || request.Choices == null)
            return;

        string id = request.Spell.SpellId;
        if (id == SpellNames.RESIST_ENERGY || id == SpellNames.PROTECTION_FROM_ENERGY)
            request.Choices.Energy = ResistEnergyType.Fire;
        else if (id == SpellNames.FIRE_SHIELD)
            request.Choices.FireShieldWarm = true;
    }
}
