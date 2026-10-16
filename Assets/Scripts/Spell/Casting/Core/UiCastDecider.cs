using System;

/// <summary>
/// The PC decider: the existing spell menu, choice dialogs and AoO prompt answer for the player. Its work is the UI
/// code in GameManager.CastDeciders.cs (Spell/Resolution): the Resist Energy, Protection from Energy and Fire Shield
/// dialogs and the weapon pickers, the touch prompt (Cast Now or Discharge Later), the AoO prompt (ShowAoOActionConfirmation, then ResolveThreatenedSpellcast), and the
/// follow-up of a finished cast (highlights, stat panels, the last combat log, ShowActionChoices or AfterAttackDelay).
/// The UI's part before a cast (the Animating sub-phase, hiding the Disguise Self selector) stays in PerformSpellCast.
/// </summary>
public sealed class UiCastDecider : ICastDecider
{
    public static readonly UiCastDecider Instance = new UiCastDecider();

    private UiCastDecider() { }

    public bool IsSynchronous => false;

    public CastChoiceFill FillChoices(GameManager gm, CastContext ctx) => gm.UiFillCastChoices(ctx);

    /// <summary>The touch prompt (Cast Now, Discharge Later, Cancel; GameManager.UiChooseTouchDelivery).</summary>
    public void ChooseTouchDelivery(GameManager gm, CastContext ctx, Action<CastTouchDelivery> answer) => gm.UiChooseTouchDelivery(ctx, answer);

    public void ChooseProvocation(GameManager gm, CastContext ctx, Action<CastProvocationAnswer> answer) => gm.UiChooseCastProvocation(ctx, answer);

    /// <summary>
    /// The player's creature gives up its save only against a harmless or beneficial spell of its own side
    /// (SpellSaveRules.IsHarmlessOrBeneficialTo; PHB p.177). The game does not ask: a standing default, as the AI's.
    /// </summary>
    public bool ForgoSave(CharacterController self, CharacterController caster, SpellData spell)
        => SpellSaveRules.IsHarmlessOrBeneficialTo(self, caster, spell);

    public void BeforeCombatEndCheck(GameManager gm, CastContext ctx) => gm.UiBeforeCastCombatEndCheck(ctx);

    public void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome) => gm.UiOnCastFinished(ctx, outcome);
}
