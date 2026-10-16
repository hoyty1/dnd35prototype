using DND35e.Identifiers;

/// <summary>
/// The NPC decider (batch 11, cast_core_batch11.md): it answers a cast's choices inline, so an NPC cast runs through the
/// cast core synchronously (GameManager.RunCastSync, from TryNPCPerformSpellCast since batch 11 step 8). Since batch 11
/// step 22 it makes the caster's choices as the AI judges them (AiCastChoiceHeuristics: the energy of the visible foes'
/// attacks, Fire Shield's form, the recipient's wielded weapon, the caster's own race; SPL-112, SPL-113). The rules code
/// reads the request and never asks who controls the caster or how the choice was made.
/// </summary>
public sealed class AiCastDecider : ICastDecider
{
    public static readonly AiCastDecider Instance = new AiCastDecider();

    private AiCastDecider() { }

    public bool IsSynchronous => true;

    /// <summary>
    /// The choices the AI cannot make yet (batch 11 step 25; AI follow-ups in docs/systems/AI.md): the ends of a wall
    /// (Wall of Fire, PHB p.298; Wall of Ice, PHB p.299; Wall of Force, PHB p.298), Grease on a held object or worn armor
    /// (PHB p.237; a Grease request with no form chosen is the same choice left open), and the square a Summon Swarm
    /// places its swarm on (PHB p.289). A request for any of these is refused at Legality, nothing spent; the AI's
    /// candidate walk then tries its next spell. Every other choice the AI fills (<see cref="FillAiChoices"/>).
    /// </summary>
    public bool CanFill(SpellCastRequest req)
    {
        SpellData spell = req != null ? req.Spell : null;
        if (spell == null)
            return true;
        string id = spell.SpellId;
        if (id == SpellNames.WALL_OF_FIRE || id == SpellNames.WALL_OF_ICE || id == SpellNames.WALL_OF_FORCE)
            return false;
        if (id == SpellNames.SUMMON_SWARM)
            return false;
        if (id == SpellNames.GREASE)
            return req.ResolveKind == CastResolveKind.Grease && req.Choices != null && req.Choices.GreaseMode == GreaseCastMode.Area;
        return true;
    }

    /// <summary>
    /// The AI's choices (<see cref="FillAiChoices"/>): Filled, or Refused when Magic Weapon, Greater Magic Weapon or Keen
    /// Edge finds no weapon the recipient wields that the spell could enchant (nothing is spent; the AI's spell choice
    /// then tries its next candidate, AIService.TryExecuteSpellcastAction). Never suspends.
    /// </summary>
    public CastChoiceFill FillChoices(GameManager gm, CastContext ctx)
        => FillAiChoices(ctx != null ? ctx.Request : null, gm != null ? gm.GetAllCharactersForAI() : null);

    /// <summary>
    /// A melee touch spell is always cast now: the NPC path never holds a charge (SPL-115; batch 11 step 27 lets the AI
    /// hold one).
    /// </summary>
    public void ChooseTouchDelivery(GameManager gm, CastContext ctx, System.Action<CastTouchDelivery> answer)
        => answer?.Invoke(CastTouchDelivery.CastNow);

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

    /// <summary>
    /// An NPC gives up its save only against a harmless or beneficial spell of its own side
    /// (SpellSaveRules.IsHarmlessOrBeneficialTo; PHB p.177), the same default as the player's creatures.
    /// </summary>
    public bool ForgoSave(CharacterController self, CharacterController caster, SpellData spell)
        => SpellSaveRules.IsHarmlessOrBeneficialTo(self, caster, spell);

    /// <summary>Nothing to do before the combat-end check.</summary>
    public void BeforeCombatEndCheck(GameManager gm, CastContext ctx) { }

    /// <summary>
    /// The NPC path's follow-up of a cast that counts as cast (GameManager.AiOnCastFinished): the result log becomes the
    /// last combat log and the stat panels refresh (which also refreshes the Flanked conditions), as the old NPC
    /// executor did before it returned. The NPC turn then goes on by itself.
    /// </summary>
    public void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome) => gm.AiOnCastFinished(ctx, outcome);

    /// <summary>
    /// Fills the caster's choices the request does not carry yet, from what the caster can see of
    /// <paramref name="all"/> (AiCastChoiceHeuristics): Resist Energy (PHB p.272) and Protection from Energy (PHB p.266)
    /// guard against the energy of the visible enemies' attacks (else the energy the recipient or caster last took, else
    /// fire); Fire Shield (PHB p.230) is chill against fire and warm otherwise; Magic Weapon (PHB p.251), Greater Magic
    /// Weapon (PHB p.241) and Keen Edge (PHB p.246, piercing or slashing only) enchant the recipient's wielded primary
    /// weapon, and refuse the cast when there is none or the spell would add nothing to it (AiCastChoiceHeuristics.AddsNothing); Disguise Self (PHB p.222) keeps the caster's own race.
    /// </summary>
    internal CastChoiceFill FillAiChoices(SpellCastRequest request, System.Collections.Generic.IList<CharacterController> all)
    {
        if (request == null || request.Spell == null || request.Choices == null)
            return CastChoiceFill.Filled;

        SpellData spell = request.Spell;
        string id = spell.SpellId;
        CharacterController caster = request.Caster;
        CharacterController recipient = request.Aim != null && request.Aim.Target != null ? request.Aim.Target : caster;
        CastChoices choices = request.Choices;

        if ((id == SpellNames.RESIST_ENERGY || id == SpellNames.PROTECTION_FROM_ENERGY) && !choices.Energy.HasValue)
            choices.Energy = AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, recipient, all);
        else if (id == SpellNames.FIRE_SHIELD && !choices.FireShieldWarm.HasValue)
            choices.FireShieldWarm = AiCastChoiceHeuristics.ChooseFireShieldWarm(caster, all);
        else if (id == SpellNames.DISGUISE_SELF && choices.DisguiseRace == null && caster != null && caster.Stats != null)
            choices.DisguiseRace = caster.ActualRace;
        else if (id == SpellNames.MAGIC_WEAPON && choices.MagicWeaponItem == null)
        {
            choices.MagicWeaponItem = AiCastChoiceHeuristics.ChooseWeapon(recipient, spell, caster);
            if (choices.MagicWeaponItem == null)
                return CastChoiceFill.Refused;
        }
        else if (id == SpellNames.GREATER_MAGIC_WEAPON && choices.GreaterMagicWeaponItem == null)
        {
            choices.GreaterMagicWeaponItem = AiCastChoiceHeuristics.ChooseWeapon(recipient, spell, caster);
            if (choices.GreaterMagicWeaponItem == null)
                return CastChoiceFill.Refused;
        }
        else if (id == SpellNames.KEEN_EDGE && choices.KeenEdgeItem == null && !choices.KeenEdgeIsAmmo)
        {
            choices.KeenEdgeItem = AiCastChoiceHeuristics.ChooseWeapon(recipient, spell, caster);
            if (choices.KeenEdgeItem == null)
                return CastChoiceFill.Refused;
        }

        return CastChoiceFill.Filled;
    }
}
