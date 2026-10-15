using UnityEngine;

/// <summary>
/// The verbal and somatic component rules of a cast (batch 11 step 13, raw-distraction-failure-stages;
/// docs/designs/cast_core_batch11.md), one rule for every caster and path: the cast core's Legality stage refuses a cast
/// that <see cref="SilenceBlocks"/>, its Failure stage rolls the deafened failure (<see cref="TryDeafenedFailure"/>) and
/// arcane spell failure (GameManager.TryRollArcaneSpellFailure, gated by <see cref="ArcaneFailureApplies"/>), and the AI's
/// spell choice skips what Silence refuses (AIService.SelectSpell), so PCs and NPCs are bound alike
/// (docs/systems/PC_NPC_PARITY.md). Pure except for the dice of <see cref="TryDeafenedFailure"/>.
///
/// RAW applied:
/// - PHB p.100-101 (Silent Spell, Still Spell): the components are those left after the cast's metamagic (or a prepared
///   metamagic spell's own record, SpellData.MetamagicDataRef), as in WildShapeRules.CanCastInCurrentForm.
/// - PHB p.279 (Silence): no spell with a verbal component can be cast inside it. Only the creature a Silence is centred
///   on is known to be under it (CharacterStats.SilenceActive; the 20-ft emanation is SPL-166).
/// - PHB p.307 (deafened): a 20% chance of spell failure when casting a spell with a verbal component, one roll per
///   casting (the deafness effect's own chance, CharacterController.GetDeafnessSpellFailureChance).
/// - PHB p.122: arcane spell failure applies only to a spell with a somatic component. DMG p.238: using a scroll is like
///   casting a spell for arcane spell failure.
/// - Delegated RAW readings 2026-10-14 (docs/systems/RULES_COVERAGE.md 4.1): a scroll keeps its spell's verbal and somatic
///   components (DMG p.213: its user still performs the closing gestures and words; DMG p.238 supplies only the material
///   components and focus), so Silence, deafness and arcane spell failure apply to it as to a cast from a slot; a wand
///   (spell trigger) needs a single spoken word and no gestures (DMG p.213), so Silence, which lets no sound issue
///   (PHB p.279), stops it, while deafness (a spell's verbal component) and arcane spell failure (gestures) do not apply;
///   the delivery of a held charge is no casting (PHB p.141), so none of these apply to it.
/// </summary>
public static class CastComponentRules
{
    /// <summary>The dice label of the deafened verbal spell failure roll (a forced-dice scenario matches it).</summary>
    public const string DeafenedFailureDiceContext = "Deafened verbal spell failure";

    /// <summary>True when the spell, after <paramref name="metamagic"/> (or its own prepared metamagic), has a verbal component.</summary>
    public static bool HasVerbalComponent(SpellData spell, MetamagicData metamagic)
    {
        if (spell == null || !spell.HasVerbalComponent)
            return false;
        MetamagicData applied = metamagic ?? spell.MetamagicDataRef;
        return applied == null || !applied.Has(MetamagicFeatId.SilentSpell);
    }

    /// <summary>True when the spell, after <paramref name="metamagic"/> (or its own prepared metamagic), has a somatic component.</summary>
    public static bool HasSomaticComponent(SpellData spell, MetamagicData metamagic)
    {
        if (spell == null || !spell.HasSomaticComponent)
            return false;
        MetamagicData applied = metamagic ?? spell.MetamagicDataRef;
        return applied == null || !applied.Has(MetamagicFeatId.StillSpell);
    }

    private static bool IsDelivery(CastSource source) => source != null && source.Kind == CastSourceKind.HeldChargeDelivery;

    private static bool IsWand(CastSource source) => source != null && source.Kind == CastSourceKind.Wand;

    /// <summary>
    /// True for a spell trigger use (a wand; a staff uses the same activation, DMG p.213, but runs outside the cast core,
    /// <see cref="SilenceBlocksSpellTrigger"/>): no casting, only a spoken word (DMG p.213).
    /// </summary>
    public static bool IsSpellTrigger(CastSource source) => IsWand(source);

    /// <summary>
    /// True when <paramref name="caster"/> stands in a Silence (PHB p.279), so a spell trigger item (a wand or a staff),
    /// whose single word must be spoken (DMG p.213), cannot be used: delegated RAW reading 2026-10-14.
    /// </summary>
    public static bool SilenceBlocksSpellTrigger(CharacterController caster)
    {
        return caster != null && caster.Stats != null && caster.Stats.SilenceActive;
    }

    /// <summary>
    /// True when <paramref name="caster"/> stands in a Silence (PHB p.279) and the cast needs sound: a spell with a verbal
    /// component left after its metamagic (Silent Spell, PHB p.100), from a slot or a scroll, or any wand use (its
    /// spoken trigger word, DMG p.213; delegated RAW reading 2026-10-14). Never for a held-charge delivery.
    /// </summary>
    public static bool SilenceBlocks(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (caster == null || caster.Stats == null || spell == null || !caster.Stats.SilenceActive || IsDelivery(source))
            return false;
        return IsWand(source) || HasVerbalComponent(spell, metamagic);
    }

    /// <summary>The log line of a Silence refusal (PHB p.279), the same on every path that refuses a cast or a wand use.</summary>
    public static string DescribeSilenceRefusal(string casterName, string spellName, bool wand)
    {
        return wand
            ? $"{casterName} cannot use the wand of {spellName} inside a Silence: its command word cannot be spoken (DMG p.213, PHB p.279)."
            : $"{casterName} cannot cast {spellName} inside a Silence: it has a verbal component (PHB p.279; Silent Spell would remove it).";
    }

    /// <summary>
    /// True when the cast makes the casting distraction checks of the cast core's Distraction stage (the entangled check,
    /// PHB p.308; grappled or pinned, PHB p.156): every casting, from a slot or a scroll (DMG p.238: activating a scroll
    /// spell is subject to disruption as casting is), but not a held-charge delivery (no casting, PHB p.141) and not a
    /// spell trigger use (a wand): delegated RAW reading 2026-10-14. Using a wand is not casting a spell (DMG p.213: no
    /// gestures, only a word; it provokes no attack of opportunity), PHB p.308 asks the check of a character who "attempts
    /// to cast a spell", and PHB p.156 lets a grappler activate any item that is not spell completion with no check,
    /// while DMG p.238 names disruption only for scrolls. The blinking caster's 20% (PHB p.206, "your own spells ... activate
    /// just as you go ethereal") is a matter of timing, not of casting, so it applies to a wand's spell too.
    /// </summary>
    public static bool CastingDistractionApplies(CastSource source)
    {
        return !IsDelivery(source) && !IsSpellTrigger(source);
    }

    /// <summary>
    /// The deafened caster's chance (percent) that the cast fails (PHB p.307): the deafness effect's chance for a spell
    /// with a verbal component left after its metamagic, from a slot or a scroll; 0 for a wand use, a held-charge
    /// delivery or a caster who hears.
    /// </summary>
    public static int DeafenedFailureChance(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (caster == null || spell == null || IsDelivery(source) || IsWand(source) || !HasVerbalComponent(spell, metamagic))
            return 0;
        return Mathf.Clamp(caster.GetDeafnessSpellFailureChance(), 0, 100);
    }

    /// <summary>
    /// Rolls the deafened verbal spell failure once for this casting (PHB p.307) when <see cref="DeafenedFailureChance"/>
    /// is above 0: d% under <see cref="DeafenedFailureDiceContext"/>, a roll at or under the chance fails. False, with
    /// <paramref name="roll"/> 0, when no roll is due.
    /// </summary>
    public static bool TryDeafenedFailure(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source,
        out int roll, out int chance)
    {
        roll = 0;
        chance = DeafenedFailureChance(caster, spell, metamagic, source);
        if (chance <= 0)
            return false;
        roll = DiceService.Percentile(DeafenedFailureDiceContext);
        return roll <= chance;
    }

    /// <summary>
    /// True when the cast is subject to arcane spell failure: an arcane spell with a somatic component left after its
    /// metamagic (Still Spell; PHB p.122, p.101). From a slot the spell is arcane when its caster is an arcane caster
    /// (CharacterStats.IsAffectedByArcaneSpellFailure); from a scroll when the scroll is arcane (<see cref="IsArcaneScroll"/>),
    /// whoever reads it, since using a scroll is like casting its spell for arcane spell failure (DMG p.238), so a wizard
    /// reading a divine scroll rolls none and a cleric or rogue reading an arcane one does. Not for a wand (no gestures,
    /// DMG p.213), a held-charge delivery (no casting, PHB p.141) or the F12 test panel's ArcaneFailure bypass. The
    /// chance itself is CharacterStats.ArcaneSpellFailure.
    /// </summary>
    public static bool ArcaneFailureApplies(CharacterController caster, SpellData spell, MetamagicData metamagic, CastSource source)
    {
        if (caster == null || caster.Stats == null || spell == null || IsDelivery(source) || IsWand(source))
            return false;
        if (source != null && source.HasBypass(TestPanelBypass.ArcaneFailure))
            return false;
        bool arcane = source != null && source.Kind == CastSourceKind.Scroll && source.Item != null
            ? IsArcaneScroll(source.Item)
            : caster.Stats.IsAffectedByArcaneSpellFailure;
        return arcane && HasSomaticComponent(spell, metamagic);
    }

    /// <summary>True when <paramref name="scroll"/> holds an arcane spell (ScrollData.IsArcane, else the legacy ScrollType).</summary>
    public static bool IsArcaneScroll(ItemData scroll)
    {
        if (scroll == null)
            return false;
        if (scroll.Scroll != null)
            return scroll.Scroll.IsArcane;
        return string.Equals(scroll.ScrollType, "Arcane", System.StringComparison.OrdinalIgnoreCase);
    }
}
