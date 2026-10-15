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

    /// <summary>The item's caster level (DMG p.237, p.245); recorded for the cast core, not read yet (SPL-016).</summary>
    public int? ItemCasterLevel;

    /// <summary>The item's save DC, when it fixes one; recorded for the cast core, not read yet (the spell clone carries it).</summary>
    public int? SaveDCOverride;

    /// <summary>The level of the prepared spell a cleric converts (SpontaneousConversion); -1 when none.</summary>
    public int ConversionLevel = -1;

    /// <summary>The SpellId of the prepared spell a cleric converts (SpontaneousConversion); null when none.</summary>
    public string SacrificedSpellId;

    /// <summary>The F12 test panel's privileges (TestPanel kind only).</summary>
    public TestPanelBypass Bypass = TestPanelBypass.None;

    public bool IsItem => Kind == CastSourceKind.Scroll || Kind == CastSourceKind.Wand;

    public bool HasBypass(TestPanelBypass bypass) => (Bypass & bypass) == bypass && bypass != TestPanelBypass.None;
}
