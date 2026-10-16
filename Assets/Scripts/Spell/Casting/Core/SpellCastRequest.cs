using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How the cast core resolves a cast once it passed the shared stages (batch 11, cast_core_batch11.md "Executor"):
/// on one target (the Mirror Image, target Blink, Globe, SpellCaster.Cast, handler and buff stages), over an area (the
/// area handler registry, since step 9), or, since step 10, by placing summoned creatures (Summon Monster, Summon
/// Nature's Ally and Summon Swarm), by greasing an area, a held object or worn armor (Grease, its mode in
/// CastChoices.GreaseMode), or by holding a touch spell's charge (Discharge Later).
/// </summary>
public enum CastResolveKind
{
    Single,
    Area,
    Summon,
    Grease,
    HoldCharge
}

/// <summary>
/// One cast, as its source built it (batch 11 step 4, cast_core_batch11.md): the caster, its own clone of the spell, the
/// metamagic, where the spell comes from, the caster's choices, the aim and the decider that makes the choices. Every
/// cast entry point builds one and opens a <see cref="CastContext"/> for it (GameManager.BeginCast), so each cast
/// carries its own state: a PC choice, an item flag or the F12 privileges of one cast cannot reach a later one.
/// Mutable on purpose: during the migration GameManager's old <c>_pending*</c> names write into the request on top of
/// the cast stack.
/// </summary>
public sealed class SpellCastRequest
{
    public CharacterController Caster;

    /// <summary>The cast's own spell: always a clone, never a database template.</summary>
    public SpellData Spell;

    public MetamagicData Metamagic;
    public CastSource Source = new CastSource();
    public CastChoices Choices = new CastChoices();
    public CastAim Aim = new CastAim();
    public ICastDecider Decider = UiCastDecider.Instance;

    /// <summary>
    /// How the core resolves the cast: one target (the default), an area (<see cref="ForUiArea"/>), a summon
    /// (<see cref="ForUiSummon"/>), Grease (<see cref="ForUiGrease"/>) or a held charge (<see cref="ForUiHoldCharge"/>).
    /// </summary>
    public CastResolveKind ResolveKind = CastResolveKind.Single;

    /// <summary>
    /// The legacy behaviours this cast keeps until a later batch 11 step fixes them (<see cref="LegacyCastQuirk"/>): set
    /// by the builder, read only by the cast core's stage methods.
    /// </summary>
    public LegacyCastQuirk Quirks;

    /// <summary>True when <paramref name="quirk"/> is set on this request.</summary>
    public bool HasQuirk(LegacyCastQuirk quirk) => (Quirks & quirk) == quirk;

    /// <summary>
    /// The clone a cast works on. It keeps the template's MetamagicDataRef reference (a prepared metamagic spell's
    /// record, which SpellData.Clone drops) and its prepared-slot flag (SpellData.IsPreparedMetamagicSlotSpell), so the
    /// cast reads exactly what it read when it held the instance itself.
    /// </summary>
    public static SpellData CloneForCast(SpellData spell)
    {
        if (spell == null)
            return null;
        SpellData clone = spell.Clone();
        clone.MetamagicDataRef = spell.MetamagicDataRef;
        clone.IsPreparedMetamagicSlotSpell = spell.IsPreparedMetamagicSlotSpell;
        return clone;
    }

    /// <summary>
    /// A PC cast decided through the UI: <paramref name="spell"/> is cloned here (pass the template or the menu's
    /// instance). With metamagic, the clone is a plain SpellData.Clone with the metamagic's pre-cast changes
    /// (SpellCaster.ApplyMetamagicToSpellData), exactly the clone the spell menu always made; without, it is
    /// <see cref="CloneForCast"/>, which reads the same as the instance the menu used to pass on.
    /// </summary>
    public static SpellCastRequest ForUi(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        bool hasMetamagic = metamagic != null && metamagic.HasAnyMetamagic;
        var request = new SpellCastRequest
        {
            Caster = caster,
            Spell = spell == null ? null : hasMetamagic ? spell.Clone() : CloneForCast(spell),
            Metamagic = metamagic,
            Source = source ?? new CastSource(),
            Decider = UiCastDecider.Instance,
            Quirks = LegacyCastQuirkSets.UiPath
        };
        if (request.Spell != null && hasMetamagic)
            SpellCaster.ApplyMetamagicToSpellData(request.Spell, metamagic);
        return request;
    }

    /// <summary>
    /// A PC cast whose spell the source has already built (a scroll's or wand's clone with its caster level, metamagic
    /// and DC): <paramref name="spell"/> is used as it is.
    /// </summary>
    public static SpellCastRequest ForUiPrebuilt(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        return new SpellCastRequest
        {
            Caster = caster,
            Spell = spell,
            Metamagic = metamagic,
            Source = source ?? new CastSource(),
            Decider = UiCastDecider.Instance,
            Quirks = LegacyCastQuirkSets.UiPath
        };
    }

    /// <summary>
    /// The PC area builder (batch 11 step 9): turns the open Ui cast <paramref name="request"/> (built by
    /// <see cref="ForUi"/> or <see cref="ForUiPrebuilt"/> when the spell, scroll, wand or F12 cast was chosen) into an area
    /// cast aimed at <paramref name="cells"/> with the creatures in them, <paramref name="targets"/> (the core's Legality
    /// stage filters both by line of effect), with no single target, and gives it the PC area path's legacy quirks
    /// (<see cref="LegacyCastQuirkSets.UiAreaPath"/>). Returns the request.
    /// </summary>
    public static SpellCastRequest ForUiArea(SpellCastRequest request, List<CharacterController> targets, HashSet<Vector2Int> cells)
    {
        if (request == null)
            return null;
        request.ResolveKind = CastResolveKind.Area;
        if (request.Aim == null)
            request.Aim = new CastAim();
        request.Aim.Target = null;
        request.Aim.AreaTargets = targets;
        request.Aim.Cells = cells;
        request.Quirks = LegacyCastQuirkSets.UiAreaPath;
        return request;
    }

    /// <summary>
    /// The PC summon builder (batch 11 step 10): turns the open Ui cast <paramref name="request"/> of a Summon Monster,
    /// Summon Nature's Ally or Summon Swarm spell, whose creature (or swarm), list level and count rule the placement
    /// prompts already wrote into its choices, into a summon cast that places it on <paramref name="destination"/>, with
    /// no target creature, and gives it the summon path's legacy quirks (<see cref="LegacyCastQuirkSets.UiSummonPath"/>).
    /// Returns the request.
    /// </summary>
    public static SpellCastRequest ForUiSummon(SpellCastRequest request, Vector2Int destination)
    {
        if (request == null)
            return null;
        request.ResolveKind = CastResolveKind.Summon;
        if (request.Aim == null)
            request.Aim = new CastAim();
        request.Aim.Target = null;
        request.Aim.Destination = destination;
        request.Quirks = LegacyCastQuirkSets.UiSummonPath;
        return request;
    }

    /// <summary>
    /// The PC Grease builder (batch 11 step 10): turns the open Ui Grease cast <paramref name="request"/>, whose mode the
    /// mode prompt wrote into its choices (CastChoices.GreaseMode, PHB p.237), into one Grease cast: an area cast records
    /// the 10-ft square's <paramref name="cells"/> and the creatures in it, <paramref name="areaTargets"/>; an object or
    /// armor cast records the creature holding or wearing the object, <paramref name="objectHolder"/> (the spell's target
    /// is the object). No target creature; the Grease path's legacy quirks (<see cref="LegacyCastQuirkSets.UiGreasePath"/>).
    /// Returns the request.
    /// </summary>
    public static SpellCastRequest ForUiGrease(SpellCastRequest request, CharacterController objectHolder,
        List<CharacterController> areaTargets, HashSet<Vector2Int> cells)
    {
        if (request == null)
            return null;
        request.ResolveKind = CastResolveKind.Grease;
        if (request.Aim == null)
            request.Aim = new CastAim();
        request.Aim.Target = null;
        request.Aim.ObjectHolder = objectHolder;
        request.Aim.AreaTargets = areaTargets;
        request.Aim.Cells = cells;
        request.Quirks = LegacyCastQuirkSets.UiGreasePath;
        return request;
    }

    /// <summary>
    /// The PC hold builder (batch 11 step 10): turns the open Ui cast <paramref name="request"/> of a melee touch spell,
    /// answered Discharge Later at the touch prompt (ICastDecider.ChooseTouchDelivery), into a hold: the cast runs the
    /// core's stages through Spend and Provocation and then stores the charge (PHB p.141, p.176). A prepared or
    /// spontaneous slot's source becomes <see cref="CastSourceKind.HeldChargeHold"/>; a cleric's conversion keeps its kind,
    /// so the hold spends the sacrificed spell (PHB p.32, p.141; batch 11 step 14, SPL-154). No target creature (the charge is delivered later, by a cast of its own);
    /// the hold path's legacy quirks (<see cref="LegacyCastQuirkSets.UiHoldPath"/>). Returns the request.
    /// </summary>
    public static SpellCastRequest ForUiHoldCharge(SpellCastRequest request)
    {
        if (request == null)
            return null;
        request.ResolveKind = CastResolveKind.HoldCharge;
        if (request.Source == null)
            request.Source = new CastSource();
        if (request.Source.Kind == CastSourceKind.PreparedSlot || request.Source.Kind == CastSourceKind.SpontaneousSlot)
        {
            request.Source.HeldFromSpontaneousSlot = request.Source.Kind == CastSourceKind.SpontaneousSlot;
            request.Source.Kind = CastSourceKind.HeldChargeHold;
        }
        if (request.Aim == null)
            request.Aim = new CastAim();
        request.Aim.Target = null;
        request.Quirks = LegacyCastQuirkSets.UiHoldPath;
        return request;
    }

    /// <summary>
    /// The source of an NPC (AI-run) cast of <paramref name="spell"/>: the slot kind the PC spell menu would give the same
    /// cast (<see cref="CastSource.SlotKindFor"/>). The AI's spell choice reads it to skip what the casting-action rules
    /// refuse (CastActionRules, AIService.SelectSpell).
    /// </summary>
    public static CastSource AiSourceFor(CharacterController caster, SpellData spell)
    {
        return new CastSource { Kind = CastSource.SlotKindFor(caster != null ? caster.Spellcasting : null, spell) };
    }

    /// <summary>
    /// An NPC (AI-run) cast of <paramref name="spell"/> at <paramref name="target"/>: no metamagic (the NPC path has
    /// none, AI-001; batch 11 step 21 lets it cast a prepared slot's metamagic), a prepared or spontaneous slot as the caster's spellcasting says, the NPC path's legacy quirks
    /// (<see cref="LegacyCastQuirkSets.AiPath"/>, batch 11 step 8) and <see cref="AiCastDecider"/>, which fills the
    /// caster's choices when the cast core asks (the Legality stage).
    /// </summary>
    public static SpellCastRequest ForAI(CharacterController caster, CharacterController target, SpellData spell)
    {
        var request = new SpellCastRequest
        {
            Caster = caster,
            Spell = CloneForCast(spell),
            Metamagic = null,
            Source = AiSourceFor(caster, spell),
            Decider = AiCastDecider.Instance,
            Quirks = LegacyCastQuirkSets.AiPath
        };
        request.Aim.Target = target;
        return request;
    }
}
