using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How the cast core resolves a cast once it passed the shared stages (batch 11, cast_core_batch11.md "Executor"):
/// on one target (the Mirror Image, target Blink, Globe, SpellCaster.Cast, handler and buff stages) or over an area (the
/// area handler registry, since step 9). Summon and held-charge resolves join in step 10.
/// </summary>
public enum CastResolveKind
{
    Single,
    Area
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

    /// <summary>How the core resolves the cast: one target (the default) or an area (<see cref="ForUiArea"/>).</summary>
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
    /// record, which SpellData.Clone drops), so the cast reads exactly what it read when it held the instance itself.
    /// </summary>
    public static SpellData CloneForCast(SpellData spell)
    {
        if (spell == null)
            return null;
        SpellData clone = spell.Clone();
        clone.MetamagicDataRef = spell.MetamagicDataRef;
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
    /// An NPC (AI-run) cast of <paramref name="spell"/> at <paramref name="target"/>: no metamagic (the NPC path has
    /// none, SPL-038), a prepared or spontaneous slot as the caster's spellcasting says, the NPC path's legacy quirks
    /// (<see cref="LegacyCastQuirkSets.AiPath"/>, batch 11 step 8) and <see cref="AiCastDecider"/>, which fills the
    /// caster's choices when the cast core asks (the Legality stage).
    /// </summary>
    public static SpellCastRequest ForAI(CharacterController caster, CharacterController target, SpellData spell)
    {
        SpellcastingComponent spellComp = caster != null ? caster.Spellcasting : null;
        var request = new SpellCastRequest
        {
            Caster = caster,
            Spell = CloneForCast(spell),
            Metamagic = null,
            Source = new CastSource
            {
                Kind = spellComp != null && spellComp.IsSpontaneousCaster ? CastSourceKind.SpontaneousSlot : CastSourceKind.PreparedSlot
            },
            Decider = AiCastDecider.Instance,
            Quirks = LegacyCastQuirkSets.AiPath
        };
        request.Aim.Target = target;
        return request;
    }
}
