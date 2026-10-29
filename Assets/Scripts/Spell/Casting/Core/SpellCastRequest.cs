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
    /// A held-charge delivery whose attack step the attack-sequence executor has paid (batch 12 step 18, SPL-156;
    /// AttackSequenceRequest.ForHeldChargeTouch): the Legality and Cost stages charge no second attack for it.
    /// </summary>
    public bool DeliveryStepCommitted;

    /// <summary>
    /// The BAB of the attack step a held-charge touch takes the place of (set with <see cref="DeliveryStepCommitted"/>):
    /// the full BAB for the attack action or a full attack's first step, the iterative BAB of a later step (PHB p.141,
    /// p.143). The touch roll uses it in place of the caster's BAB (SpellCaster.SpellTouchAttack). Null: the full BAB.
    /// </summary>
    public int? DeliveryStepBab;

    /// <summary>The touch roll's BAB change for <see cref="DeliveryStepBab"/> (0 or less; 0 when unset).</summary>
    public int TouchStepBabAdjustment
        => DeliveryStepBab.HasValue && Caster != null && Caster.Stats != null
            ? Mathf.Min(0, DeliveryStepBab.Value - Caster.Stats.BaseAttackBonus)
            : 0;

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
            Decider = UiCastDecider.Instance
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
            Decider = UiCastDecider.Instance
        };
    }

    /// <summary>
    /// The PC area builder (batch 11 step 9): turns the open Ui cast <paramref name="request"/> (built by
    /// <see cref="ForUi"/> or <see cref="ForUiPrebuilt"/> when the spell, scroll, wand or F12 cast was chosen) into an area
    /// cast aimed at <paramref name="cells"/> with the creatures in them, <paramref name="targets"/> (the core's Legality
    /// stage filters both by line of effect), with no single target. <paramref name="origin"/> is the area's point of origin, the grid
    /// intersection the UI aimed it from (<see cref="CastAim.AreaOrigin"/>; batch 11 step 24), null for the walls. Returns
    /// the request.
    /// </summary>
    public static SpellCastRequest ForUiArea(SpellCastRequest request, List<CharacterController> targets, HashSet<Vector2Int> cells,
        GridIntersection? origin = null)
    {
        if (request == null)
            return null;
        request.ResolveKind = CastResolveKind.Area;
        if (request.Aim == null)
            request.Aim = new CastAim();
        request.Aim.Target = null;
        request.Aim.AreaTargets = targets;
        request.Aim.Cells = cells;
        request.Aim.AreaOrigin = origin;
        return request;
    }

    /// <summary>
    /// The PC summon builder (batch 11 step 10): turns the open Ui cast <paramref name="request"/> of a Summon Monster,
    /// Summon Nature's Ally or Summon Swarm spell, whose creature (or swarm), list level and count rule the placement
    /// prompts already wrote into its choices, into a summon cast that places it on <paramref name="destination"/>, with
    /// no target creature. Returns the request.
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
        return request;
    }

    /// <summary>
    /// The PC Grease builder (batch 11 step 10): turns the open Ui Grease cast <paramref name="request"/>, whose mode the
    /// mode prompt wrote into its choices (CastChoices.GreaseMode, PHB p.237), into one Grease cast: an area cast records
    /// the 10-ft square's <paramref name="cells"/> and the creatures in it, <paramref name="areaTargets"/>; an object or
    /// armor cast records the creature holding or wearing the object, <paramref name="objectHolder"/> (the spell's target
    /// is the object). No target creature.
    /// An area cast records its square's centre, <paramref name="areaOrigin"/>, as its point of origin, from which the
    /// Legality stage traces line of effect (batch 11 step 24, SPL-006). Returns the request.
    /// </summary>
    public static SpellCastRequest ForUiGrease(SpellCastRequest request, CharacterController objectHolder,
        List<CharacterController> areaTargets, HashSet<Vector2Int> cells, GridIntersection? areaOrigin = null)
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
        request.Aim.AreaOrigin = areaOrigin;
        return request;
    }

    /// <summary>
    /// The PC hold builder (batch 11 step 10): turns the open Ui cast <paramref name="request"/> of a melee touch spell,
    /// answered Discharge Later at the touch prompt (ICastDecider.ChooseTouchDelivery), into a hold: the cast runs the
    /// core's stages through Spend and Provocation and then stores the charge (PHB p.141, p.176). A prepared or
    /// spontaneous slot's source becomes <see cref="CastSourceKind.HeldChargeHold"/>; a cleric's conversion keeps its kind,
    /// so the hold spends the sacrificed spell (PHB p.32, p.141; batch 11 step 14, SPL-154). No target creature (the charge is delivered later, by a cast of its own);
    /// Returns the request.
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
    /// The metamagic an NPC (AI-run) cast of <paramref name="spell"/> carries without a choice of its own: the preparation
    /// metamagic of a prepared metamagic slot's spell (SpellData.IsPreparedMetamagicSlotSpell, which
    /// SpellcastingComponent.GetCastablePreparedSpells lists for the AI as the spell menu lists it for a PC), or the
    /// metamagic the AI chose for a spontaneous cast (SpellData.AiChosenMetamagic, on the AI's candidate clone of a known
    /// spell, AiMetamagicPlanner; batch 11 step 28, AI-001), else none. The AI's spell choice, its legality probes and the
    /// NPC cast read the same (batch 11 step 21).
    /// </summary>
    public static MetamagicData AiMetamagicFor(SpellData spell)
    {
        if (spell == null)
            return null;
        if (spell.AiChosenMetamagic != null && spell.AiChosenMetamagic.HasAnyMetamagic)
            return spell.AiChosenMetamagic;
        return spell.IsPreparedMetamagicSlotSpell && spell.MetamagicDataRef != null && spell.MetamagicDataRef.HasAnyMetamagic
            ? spell.MetamagicDataRef
            : null;
    }

    /// <summary>
    /// An NPC (AI-run) or Scripted cast of <paramref name="spell"/> at <paramref name="target"/>: with
    /// <paramref name="metamagic"/> when the caller gives one (a Scripted cast step's), else the prepared metamagic slot's
    /// (<see cref="AiMetamagicFor"/>; batch 11 step 21: the NPC path casts metamagic as the PC menu does, a clone with the
    /// metamagic's pre-cast changes, SpellCaster.ApplyMetamagicToSpellData, and spends the slot or spell per day the
    /// metamagic spell takes); a prepared or spontaneous slot as the caster's spellcasting says, and
    /// <see cref="AiCastDecider"/>, which fills the
    /// caster's choices when the cast core asks (the Legality stage).
    /// </summary>
    public static SpellCastRequest ForAI(CharacterController caster, CharacterController target, SpellData spell, MetamagicData metamagic = null)
    {
        MetamagicData castMetamagic = metamagic != null && metamagic.HasAnyMetamagic ? metamagic : AiMetamagicFor(spell);
        bool hasMetamagic = castMetamagic != null;
        var request = new SpellCastRequest
        {
            Caster = caster,
            Spell = spell == null ? null : hasMetamagic ? spell.Clone() : CloneForCast(spell),
            Metamagic = hasMetamagic ? castMetamagic.Clone() : null,
            Source = AiSourceFor(caster, spell),
            Decider = AiCastDecider.Instance
        };
        if (request.Spell != null && hasMetamagic)
            SpellCaster.ApplyMetamagicToSpellData(request.Spell, request.Metamagic);
        request.Aim.Target = target;
        return request;
    }

    /// <summary>
    /// An NPC (AI-run) area cast of <paramref name="spell"/> (batch 11 step 25, AI-001 area half): the request
    /// <see cref="ForAI"/> builds (the clone with its metamagic, the slot source, the AI decider), turned into an area cast
    /// aimed at <paramref name="cells"/> from <paramref name="origin"/> (the point of origin the AI's aim search chose,
    /// AreaAimSearch; null for a one-square effect) with the creatures in it, <paramref name="targets"/> (the core's
    /// Legality stage filters both by line of effect, as for a PC's area), and no single target. With null cells it is the request the AI's search and
    /// legality filter start from (its spell clone is the one the cast will use, so a Widened area is searched widened).
    /// </summary>
    public static SpellCastRequest ForAIArea(CharacterController caster, SpellData spell, HashSet<Vector2Int> cells,
        List<CharacterController> targets, GridIntersection? origin, MetamagicData metamagic = null)
    {
        SpellCastRequest request = ForAI(caster, null, spell, metamagic);
        request.ResolveKind = CastResolveKind.Area;
        request.Aim.Target = null;
        request.Aim.Cells = cells;
        request.Aim.AreaTargets = targets ?? new List<CharacterController>();
        request.Aim.AreaOrigin = origin;
        return request;
    }

    /// <summary>
    /// An NPC (AI-run) or Scripted summon cast of a Summon Monster or Summon Nature's Ally spell (PHB p.285-288; batch 11
    /// step 27, SPL-091): the <see cref="ForAI"/> request (the clone with its metamagic, the slot source, the AI decider)
    /// turned into a summon cast with no target creature. The creature, its list level and count rule and the square it
    /// appears on are the caster's choices, which the AI decider fills at the Legality stage (AiCastDecider.FillChoices,
    /// GameManager.AiFillSummonChoices), unless the caller set them (<paramref name="destination"/>). The core's Summon
    /// resolve then places the creatures exactly as for a PC.
    /// </summary>
    public static SpellCastRequest ForAISummon(CharacterController caster, SpellData spell, Vector2Int? destination = null, MetamagicData metamagic = null)
    {
        SpellCastRequest request = ForAI(caster, null, spell, metamagic);
        request.ResolveKind = CastResolveKind.Summon;
        request.Aim.Target = null;
        request.Aim.Destination = destination;
        return request;
    }

    /// <summary>
    /// An NPC (AI-run) hold of a melee touch spell's charge (PHB p.141, p.176; batch 11 step 27, SPL-115): the
    /// <see cref="ForAI"/> request turned into a hold exactly as <see cref="ForUiHoldCharge"/> turns a PC's (a prepared or
    /// spontaneous slot's source becomes <see cref="CastSourceKind.HeldChargeHold"/>), with no target creature. The AI holds only when its target is
    /// out of reach and it can move into reach this turn (AiCastDecider.TouchDeliveryFor).
    /// </summary>
    public static SpellCastRequest ForAIHoldCharge(CharacterController caster, SpellData spell, MetamagicData metamagic = null)
    {
        SpellCastRequest request = ForUiHoldCharge(ForAI(caster, null, spell, metamagic));
        request.Decider = AiCastDecider.Instance;
        return request;
    }

    /// <summary>
    /// The delivery of the charge <paramref name="caster"/> holds (SpellcastingComponent.HeldTouchSpell) to
    /// <paramref name="target"/> by an NPC (AI-run) caster (batch 11 step 27, SPL-115): the same request as the PC's
    /// Discharge button builds (GameManager.BuildHeldChargeDeliveryRequest: a clone of the held spell as it was cast, a
    /// held-charge-delivery source, a copy of the choices made at the hold), with the AI decider. Its cost is the PC's
    /// (HeldChargeDeliveryRules, batch 12 step 18, SPL-156); a foe's touch after the casting round runs through the
    /// attack-sequence executor (GameManager.TryNPCDeliverHeldCharge, AttackSequenceRequest.ForHeldChargeTouch). A missed
    /// touch keeps the charge for every caster (PHB p.141; the PostCast stage). Null with no charge held.
    /// </summary>
    public static SpellCastRequest ForAIHeldChargeDelivery(CharacterController caster, CharacterController target)
    {
        SpellcastingComponent spellComp = caster != null ? caster.Spellcasting : null;
        if (spellComp == null || spellComp.HeldTouchSpell == null)
            return null;
        var request = new SpellCastRequest
        {
            Caster = caster,
            Spell = CloneForCast(spellComp.HeldTouchSpell),
            Metamagic = spellComp.HeldTouchMetamagic,
            Source = new CastSource { Kind = CastSourceKind.HeldChargeDelivery },
            Decider = AiCastDecider.Instance
        };
        if (spellComp.HeldTouchChoices != null)
            request.Choices = spellComp.HeldTouchChoices.Clone();
        request.Aim.Target = target;
        return request;
    }

    /// <summary>
    /// An NPC (AI-run) Grease on a 10-ft. square (PHB p.237; batch 11 step 25): the <see cref="ForAI"/> request turned into
    /// a Grease cast in its area form (CastChoices.GreaseMode Area, the only form the AI decider can fill,
    /// AiCastDecider.CanFill) on <paramref name="cells"/>, whose point of origin is the square's centre,
    /// <paramref name="origin"/>, with the creatures in it, <paramref name="targets"/>.
    /// </summary>
    public static SpellCastRequest ForAIGrease(CharacterController caster, SpellData spell, HashSet<Vector2Int> cells,
        List<CharacterController> targets, GridIntersection? origin, MetamagicData metamagic = null)
    {
        SpellCastRequest request = ForAI(caster, null, spell, metamagic);
        request.ResolveKind = CastResolveKind.Grease;
        request.Choices.GreaseMode = GreaseCastMode.Area;
        request.Aim.Target = null;
        request.Aim.ObjectHolder = null;
        request.Aim.Cells = cells;
        request.Aim.AreaTargets = targets ?? new List<CharacterController>();
        request.Aim.AreaOrigin = origin;
        return request;
    }
}
