using System;

/// <summary>
/// Where a cast's spell comes from (batch 11, docs/designs/cast_core_batch11.md). The source decides what the cast spends
/// and which item or test-panel privileges it carries; it travels with its own <see cref="SpellCastRequest"/>, so a
/// cancelled scroll, wand or F12 cast can no longer leave a flag that a later cast reads (SPL-116, SPL-036).
/// </summary>
public enum CastSourceKind
{
    /// <summary>A prepared caster's slot (or a caster with no slot system, such as a scratch Ui cast).</summary>
    PreparedSlot,
    /// <summary>A spontaneous caster's spell per day (sorcerer, bard).</summary>
    SpontaneousSlot,
    /// <summary>A cleric's spontaneous cure or inflict conversion of a prepared spell (PHB p.32).</summary>
    SpontaneousConversion,
    /// <summary>A scroll read through the cast pipeline (spell completion, DMG p.237).</summary>
    Scroll,
    /// <summary>A wand used through the cast pipeline (spell trigger, DMG p.245).</summary>
    Wand,
    /// <summary>A melee touch spell cast to hold its charge (Discharge Later, PHB p.176).</summary>
    HeldChargeHold,
    /// <summary>The delivery of a held touch charge (the Discharge button).</summary>
    HeldChargeDelivery,
    /// <summary>The F12 Spell Testing Panel (a developer tool, not play): its privileges are <see cref="TestPanelBypass"/>.</summary>
    TestPanel
}

/// <summary>The F12 test panel's explicit privileges on its cast (cast_core_batch11.md: every F12 privilege is a flag).</summary>
[Flags]
public enum TestPanelBypass
{
    None = 0,
    /// <summary>No spell component pouch and no costly components (the pouch check, Stoneskin's diamond dust).</summary>
    Components = 1,
    /// <summary>No arcane spell failure roll.</summary>
    ArcaneFailure = 2,
    /// <summary>Unlimited slots (requested by the panel; today only logged, SPL-036).</summary>
    Slots = 4
}

/// <summary>The source of one cast: its kind, the item it uses, the cleric conversion it makes and the F12 privileges.</summary>
public sealed class CastSource
{
    public CastSourceKind Kind = CastSourceKind.PreparedSlot;

    /// <summary>The scroll or wand (Scroll and Wand kinds); null once the item has been spent for this cast.</summary>
    public ItemData Item;

    /// <summary>The item's slot in the caster's pack; -1 when none.</summary>
    public int ItemIndex = -1;

    /// <summary>The item's caster level (DMG p.237, p.245): CasterLevelRules.For gives it to the cast (batch 11 step 14).</summary>
    public int? ItemCasterLevel;

    /// <summary>The item's save DC, when it fixes one; recorded for the cast core, not read yet (the spell clone carries it).</summary>
    public int? SaveDCOverride;

    /// <summary>The level of the prepared spell a cleric converts (SpontaneousConversion); -1 when none.</summary>
    public int ConversionLevel = -1;

    /// <summary>The SpellId of the prepared spell a cleric converts (SpontaneousConversion); null when none.</summary>
    public string SacrificedSpellId;

    /// <summary>The F12 test panel's privileges (TestPanel kind only).</summary>
    public TestPanelBypass Bypass = TestPanelBypass.None;

    /// <summary>
    /// True for a hold (<see cref="CastSourceKind.HeldChargeHold"/>) whose spell comes from a spontaneous caster's spell
    /// per day: SpellCastRequest.ForUiHoldCharge records it when it turns a SpontaneousSlot source into a hold, so the
    /// hold's casting stays spontaneous for the metamagic rules (<see cref="IsSpontaneous"/>; batch 11 step 12).
    /// </summary>
    public bool HeldFromSpontaneousSlot;

    public bool IsItem => Kind == CastSourceKind.Scroll || Kind == CastSourceKind.Wand;

    /// <summary>
    /// True when the spell is cast spontaneously (PHB p.88 and p.98): a spontaneous caster's spell per day, a cleric's
    /// cure or inflict conversion, or a hold of a spontaneous caster's spell. The source decides, not the caster's
    /// classes (delegated RAW reading 2026-10-14): MetamagicSystem makes such a metamagic cast a full-round action and
    /// refuses Quicken Spell on it. The slot kind itself is still picked per caster (<see cref="SlotKindFor"/>, SPL-159).
    /// </summary>
    public bool IsSpontaneous => Kind == CastSourceKind.SpontaneousSlot || Kind == CastSourceKind.SpontaneousConversion
                                 || (Kind == CastSourceKind.HeldChargeHold && HeldFromSpontaneousSlot);

    public bool HasBypass(TestPanelBypass bypass) => (Bypass & bypass) == bypass && bypass != TestPanelBypass.None;

    /// <summary>
    /// The slot kind a cast of <paramref name="spell"/> by the caster of <paramref name="spellComp"/> spends: the one
    /// helper the PC spell menu (GameManager.BuildSpellMenuCastSource), the metamagic panel (CombatUI) and the NPC path
    /// (SpellCastRequest.AiSourceFor) share, so all three agree on whether a cast is spontaneous (PHB p.88, p.98).
    /// Today a caster with a spontaneous class casts every spell from its spells per day: the spell menu lists only
    /// those and SpellcastingComponent spends there (SPL-011), so the answer is per caster and
    /// <paramref name="spell"/> is not read yet. When SPL-011 dispatches on the chosen spell's casting class, this is the
    /// place to return PreparedSlot for a prepared class's spell of a multiclass caster (SPL-159).
    /// </summary>
    public static CastSourceKind SlotKindFor(SpellcastingComponent spellComp, SpellData spell)
    {
        return spellComp != null && spellComp.IsSpontaneousCaster ? CastSourceKind.SpontaneousSlot : CastSourceKind.PreparedSlot;
    }
}
