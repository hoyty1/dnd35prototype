using System;

/// <summary>
/// Behaviours of today's cast paths that the staged cast core keeps on purpose until a later batch 11 step fixes them
/// (docs/designs/cast_core_batch11.md, "Migration rule"). Each bit names one known deviation and cites its issue. Request
/// builders set the bits (<see cref="SpellCastRequest.ForUi"/> and <see cref="SpellCastRequest.ForUiPrebuilt"/> set
/// <see cref="LegacyCastQuirkSets.UiPath"/>, the PC single-target path as it is today); only the cast core's stage
/// methods (Spell/Resolution/GameManager.CastCore.cs) read them. A RAW-fix step removes a family of bits on every path at
/// once. A bit whose RAW behaviour the core does not have yet is required by the stage that would need it: a request
/// without it raises a cast-core violation and gets the legacy behaviour anyway (GameManager.RequireLegacyQuirk).
/// </summary>
[Flags]
public enum LegacyCastQuirk
{
    None = 0,

    /// <summary>
    /// The PC single-target cast runs the combat-end check only when its target was killed or is out of the fight, not
    /// after every cast (CORE-039; the core's end state is "CombatEnd: EvaluateCombatEnd, always", which the NPC cast
    /// wrapper TryNPCPerformSpellCastForAI already does for every cast that returns true). Another end is caught at the
    /// next ShowActionChoices or turn boundary. Batch 11 step 11 (raw-failed-cast-gating) removes the bit. Without the
    /// bit the core evaluates after every resolved cast.
    /// </summary>
    EvaluateCombatEndOnlyOnTargetDeath = 1 << 0,

    /// <summary>
    /// The Globe of Invulnerability test runs after SpellCaster.Cast has applied damage and healing, at the base spell
    /// level (SPL-141; batch 11 step 11 moves it before the effect). Required: the core has no pre-cast globe stage yet.
    /// </summary>
    GlobeAfterCast = 1 << 1,

    /// <summary>
    /// The entangled casting check rolls d20 + caster level + casting ability under the "Dispel Magic check" label, and
    /// only for somatic spells (SPL-145; batch 11 step 13 makes it a Concentration check for any spell). Required.
    /// </summary>
    EntangledUsesCasterLevelFormula = 1 << 2,

    /// <summary>
    /// A threatened quickened cast goes through the provocation prompt and provokes (SPL-143; PHB p.98; batch 11
    /// step 12). Without the bit the core skips the provocation stage for a quickened cast.
    /// </summary>
    QuickenedProvokes = 1 << 3,

    /// <summary>
    /// A cast whose slot spend fails (or whose caster has no SpellcastingComponent) after the action was spent clears
    /// the resource snapshot, so the action is lost (SPL-147; batch 11 step 14). Without the bit the core restores it.
    /// </summary>
    SlotFailureClearsSnapshot = 1 << 4,

    /// <summary>
    /// The casting-while-concentrating step (HandleConcentrationOnCasting) runs before the provocation prompt, so a cast
    /// cancelled there has already rolled it (SPL-152; batch 11 step 12). Without the bit it runs once the provocation
    /// resolves to proceed.
    /// </summary>
    ConcentrationGateBeforeProvocation = 1 << 5,

    /// <summary>
    /// The PC range highlight (ShowSpellTargets) measures range by character level, not caster level (SPL-017; batch 11
    /// step 14). Not read by a stage yet: the range test is still the targeting highlight's; the Legality stage takes it
    /// over with CasterLevelRules.
    /// </summary>
    RangeHighlightUsesCharacterLevel = 1 << 6,

    /// <summary>
    /// The Sound Burst stun handler runs (gate None) when the cast failed, and rolls its own Fortitude save (SPL-142;
    /// batch 11 step 11). Required for a Sound Burst cast.
    /// </summary>
    SoundBurstStunIgnoresSuccess = 1 << 7,

    /// <summary>
    /// The Animate Rope handler runs (gate None) and entangles when the cast failed without an attack roll (SPL-142;
    /// batch 11 step 11). Required for an Animate Rope cast.
    /// </summary>
    AnimateRopeIgnoresSuccess = 1 << 8,

    /// <summary>
    /// The caster and every ally that is not charmed or confused forgo their save against every spell, harmful ones
    /// included (ShouldForceTargetToAcceptSave; SPL-149, PHB p.177; batch 11 step 18). Required whenever it makes the
    /// target forgo a save.
    /// </summary>
    AlliesForgoEverySave = 1 << 9,

    /// <summary>
    /// The deafened 20% verbal spell failure is rolled inside SpellCaster.Cast, after the slot is spent, not once in the
    /// Failure stage (SPL-150; batch 11 step 13). Required for a deafened caster.
    /// </summary>
    DeafenedRolledInSpellCaster = 1 << 10,

    /// <summary>
    /// A scroll cast rolls no arcane spell failure (TryRollArcaneSpellFailure; SPL-116, DMG p.238; batch 11 step 13).
    /// Required for a scroll cast.
    /// </summary>
    ScrollSkipsArcaneFailure = 1 << 11,

    /// <summary>
    /// Delivering a held touch charge costs no action in any round (SPL-156). RAW: the touch in the round of casting
    /// comes with the casting; after that round a delivery to a foe is a melee touch attack made instead of a regular
    /// attack, so it takes the attack action or one attack of a full attack (delegated RAW reading 2026-10-14), and
    /// touching one friend is a standard action, up to six friends a full-round action (PHB p.141, Holding the Charge).
    /// The core's Cost stage (batch 11 plan) removes the bit. Required for a held-charge delivery.
    /// </summary>
    HeldChargeDeliveryIsFree = 1 << 12,

    /// <summary>
    /// The Cost stage honours Quicken before the spontaneous full-round rule, so a quickened spontaneous cast would cost
    /// no action, where Quicken Spell cannot be applied to a spontaneous cast at all (SPL-144, PHB p.98 Special; batch 11
    /// steps 12 and 21). Required for a quickened cast.
    /// </summary>
    QuickenBeforeSpontaneousFullRound = 1 << 13,

    /// <summary>
    /// The Cost stage decides the full-round cost of a metamagic cast by the caster's class
    /// (MetamagicSystem.IsSpontaneousCaster: any Sorcerer or Bard level), not by the cast's source, so a Wizard/Sorcerer
    /// pays a full round for a prepared metamagic spell and a cleric's metamagic conversion would cost a standard action
    /// (SPL-159, PHB p.88; batch 11 step 12, raw-action-cost-provocation). Required for a metamagic cast that is not
    /// quickened.
    /// </summary>
    SpontaneousFullRoundByCasterClass = 1 << 14,
}

/// <summary>The quirk sets the request builders give (batch 11 step 7 on).</summary>
public static class LegacyCastQuirkSets
{
    /// <summary>Today's PC single-target path (PerformSpellCast through the cast core with the UI decider).</summary>
    public const LegacyCastQuirk UiPath =
        LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath
        | LegacyCastQuirk.GlobeAfterCast
        | LegacyCastQuirk.EntangledUsesCasterLevelFormula
        | LegacyCastQuirk.QuickenedProvokes
        | LegacyCastQuirk.SlotFailureClearsSnapshot
        | LegacyCastQuirk.ConcentrationGateBeforeProvocation
        | LegacyCastQuirk.RangeHighlightUsesCharacterLevel
        | LegacyCastQuirk.SoundBurstStunIgnoresSuccess
        | LegacyCastQuirk.AnimateRopeIgnoresSuccess
        | LegacyCastQuirk.AlliesForgoEverySave
        | LegacyCastQuirk.DeafenedRolledInSpellCaster
        | LegacyCastQuirk.ScrollSkipsArcaneFailure
        | LegacyCastQuirk.HeldChargeDeliveryIsFree
        | LegacyCastQuirk.QuickenBeforeSpontaneousFullRound
        | LegacyCastQuirk.SpontaneousFullRoundByCasterClass;
}
