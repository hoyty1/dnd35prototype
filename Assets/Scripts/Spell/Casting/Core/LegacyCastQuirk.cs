using System;

/// <summary>
/// Behaviours of today's cast paths that the staged cast core keeps on purpose until a later batch 11 step fixes them
/// (docs/designs/cast_core_batch11.md, "Migration rule"). Each bit names one known deviation and cites its issue. Request
/// builders set the bits (<see cref="SpellCastRequest.ForUi"/> and <see cref="SpellCastRequest.ForUiPrebuilt"/> set
/// <see cref="LegacyCastQuirkSets.UiPath"/>, the PC single-target path as it is today,
/// <see cref="SpellCastRequest.ForUiArea"/> sets <see cref="LegacyCastQuirkSets.UiAreaPath"/>, the PC area path, and
/// <see cref="SpellCastRequest.ForAI"/> sets <see cref="LegacyCastQuirkSets.AiPath"/>, the NPC one); only the cast core's stage
/// methods (Spell/Resolution/GameManager.CastCore.cs) read them. A RAW-fix step removes a family of bits on every path at
/// once. A bit whose RAW behaviour the core does not have yet is required by the stage that would need it: a request
/// without it raises a cast-core violation and gets the legacy behaviour anyway (GameManager.RequireLegacyQuirk).
/// </summary>
[Flags]
public enum LegacyCastQuirk : long
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

    // The NPC path's own behaviours (batch 11 step 8): set only by SpellCastRequest.ForAI (LegacyCastQuirkSets.AiPath).
    // Each names a PC/NPC parity issue; the RAW-fix step that removes it applies one rule to both sides.

    /// <summary>
    /// The NPC path's own legality list (SPL-160): before the shared target test the Legality stage refuses, with a
    /// trace mark and no log line, a caster with no standard action left, a dead target, a caster that cannot cast
    /// spells and a spell the caster has no slot for (SpellcastingComponent.CanCast); the target test's refusal is not
    /// logged; and the Cost stage refuses a cast whose standard action cannot be committed. The PC path leaves these
    /// to the spell menu. Batch 11 step 22 (ai-single-target-decider) moves the AI's tests into CheckLegality for every
    /// caster and removes the bit.
    /// </summary>
    NpcOwnLegalityChecks = 1 << 15,

    /// <summary>
    /// The NPC range test: the Legality stage refuses a target beyond the spell's range at the caster's caster level
    /// (GetCasterLevel, at least 1 square), measured to the target's anchor square. The PC range is the targeting
    /// highlight's, by character level and to any footprint square (<see cref="RangeHighlightUsesCharacterLevel"/>;
    /// SPL-017). Batch 11 step 14 (raw-resources-caster-level) gives both one CasterLevelRules range.
    /// </summary>
    NpcRangeUsesCasterLevel = 1 << 16,

    /// <summary>
    /// The Legality stage refuses every spell whose target type is Area (AI-001; Tests.Magic.SpellRoutingGoldenTests
    /// mirrors this data refusal). Batch 11 step 25 (npc-area-casting) lets NPCs cast area spells and removes the bit.
    /// </summary>
    NpcRefusesArea = 1 << 17,

    /// <summary>
    /// The cast runs without metamagic whatever the request carries: the Legality stage drops it (AI-001, SPL-038).
    /// Batch 11 step 21 (metamagic-legality-and-slots) casts a prepared metamagic slot's spell with its metamagic.
    /// </summary>
    NpcNullMetamagic = 1 << 18,

    /// <summary>
    /// No material-component test (the spell component pouch, PHB p.174 and p.130): NPCs carry no pouch (SPL-114).
    /// Batch 11 step 17 (raw-components-all-casters, decision D3) gives every caster its components and removes the bit.
    /// </summary>
    NpcNoComponentCheck = 1 << 19,

    /// <summary>
    /// No casting-while-concentrating step (HandleConcentrationOnCasting): the ConcentrationOnCast stage is skipped, and
    /// a spell lost to a distraction or a spell failure does not run the step either (SPL-114). Batch 11 step 15
    /// (raw-counterspell-order-parity) removes the bit.
    /// </summary>
    NpcNoConcentrationOnCastCheck = 1 << 20,

    /// <summary>
    /// A Concentration-duration effect the Buff stage applies starts no concentration tracking
    /// (BeginConcentrationTracking), so it never ends by damage or by the caster's choice (SPL-114). Batch 11 step 15
    /// removes the bit.
    /// </summary>
    NpcNoConcentrationTracking = 1 << 21,

    /// <summary>
    /// The hostile-cast invisibility break runs right after the provocation, before the counterspell and the Ring of
    /// Counterspells, so a countered NPC spell still ends the caster's invisibility (SPL-118). Batch 11 step 15 applies
    /// decision D4's one order to every caster and removes the bit.
    /// </summary>
    NpcInvisibilityBreakBeforeCounterspell = 1 << 22,

    /// <summary>
    /// The Spend stage runs right after the distraction checks, before the Failure stage, so the arcane spell failure
    /// and the blinking caster's 20% are rolled with the slot already spent (and spend nothing more), and a slot that
    /// cannot be spent ends the cast without an error log (SPL-160). Batch 11 step 13 (raw-distraction-failure-stages)
    /// makes the failure checks one rule on every path and removes the bit.
    /// </summary>
    NpcSpendsBeforeFailureChecks = 1 << 23,

    /// <summary>
    /// The blinking caster's and the blinking target's percentile rolls carry the NPC dice labels ("Blink NPC caster
    /// spell failure", "Blink NPC target spell failure"), and a passed Blink check logs no line (SPL-160). Batch 11 step
    /// 13 gives both one Blink rule and one label and removes the bit.
    /// </summary>
    NpcBlinkLabelsAndSilentPass = 1 << 24,

    /// <summary>
    /// The PostCast stage shows the cast's result log before the target's death (OnDeath and the summon cleanup) instead
    /// of after it (SPL-160). Batch 11 step 11 (raw-failed-cast-gating), which settles the CombatEnd stage, removes it.
    /// </summary>
    NpcResultLogBeforeTargetDeath = 1 << 25,

    /// <summary>
    /// The CombatEnd stage runs no combat-end check: the NPC wrapper TryNPCPerformSpellCastForAI runs it after every
    /// cast that counts as cast (CastOutcome.WasCast), once the stat panels are refreshed, failed casts included
    /// (CORE-039). Batch 11 step 11 puts one check after every cast in the stage for every caster and removes the bit.
    /// </summary>
    NpcCombatEndCheckedByCaller = 1 << 26,

    /// <summary>
    /// The Mirror Image end-of-turn swap is offered only to controllable casters, so an NPC's figments keep their places
    /// (SPL-117). Not read by a stage: the swap is an end-of-turn action outside the cast core
    /// (TryBeginMirrorImageSwapSelection). Batch 11 step 26 (raw-mirror-image, decision D7) removes the swap and the bit.
    /// </summary>
    NpcNoMirrorImageSwap = 1 << 27,

    // The PC area path's own behaviours (batch 11 step 9): set only by SpellCastRequest.ForUiArea
    // (LegacyCastQuirkSets.UiAreaPath). Each names the issue of what an area cast skips or does differently today.

    /// <summary>
    /// An area cast makes no entangled casting check (SPL-006; the check itself is SPL-145): the Distraction stage skips
    /// it. Batch 11 step 13 (raw-distraction-failure-stages) gives every cast one entangled rule and removes the bit.
    /// </summary>
    AreaSkipsEntangled = 1L << 28,

    /// <summary>
    /// A blinking caster's area spell has no 20% spell failure (PHB p.206; SPL-006): the Failure stage skips the roll.
    /// Batch 11 step 13 removes the bit.
    /// </summary>
    AreaSkipsBlink = 1L << 29,

    /// <summary>
    /// An area cast is never tested against a Ring of Counterspells (DMG p.230; SPL-006): the RingCounterspell stage is
    /// skipped (an area request has no single target for it to test). The ring counters its stored spell when that spell
    /// is cast on the wearer; RAW does not say whether an area spell whose area takes in the wearer counts, nor whether the
    /// ring then stops the whole spell or only its effect on the wearer. So this bit is not yet a known deviation: batch 11
    /// step 15 (raw-counterspell-order-parity) must decide that reading under the owner's delegation and record it in
    /// docs/systems/RULES_COVERAGE.md 4.1 before it removes the bit.
    /// </summary>
    AreaSkipsRing = 1L << 30,

    /// <summary>
    /// An area cast makes no spell component pouch test (PHB p.174 and p.130; SPL-006, SPL-114): the Legality stage's
    /// component test passes it. Batch 11 step 17 (raw-components-all-casters, decision D3) removes the bit.
    /// </summary>
    AreaSkipsPouch = 1L << 31,

    /// <summary>
    /// Only some area handlers end their cast with a combat-end check (EvaluateCombatEnd): the generic loop, the scaled
    /// damage spells (Fireball, Lightning Bolt, Call Lightning), Halt Undead, Hypnotism, Sleep, Deep Slumber, Color
    /// Spray, Fear, Ice Storm, Shout, Cone of Cold, Chain Lightning, Circle of Death, Insect Plague, Wall of Thorns,
    /// Sunburst and Earthquake (AreaHandlerEntry.CombatEndContext); the other handlers end without one, so an end they
    /// cause is caught at the next ShowActionChoices or turn boundary (CORE-039). Without the bit FinishAreaCast checks
    /// after every area cast. Batch 11 step 11 (raw-failed-cast-gating) removes the bit with CORE-039.
    /// </summary>
    AreaCombatEndOnlyInSomeHandlers = 1L << 32,

    /// <summary>
    /// The Hypnotism, Sleep, Deep Slumber, Color Spray and Fear handlers log no quickened or spontaneous line
    /// (AreaHandlerEntry.OmitsLogPrefix; SPL-161): FinishAreaCast leaves the cast's log prefix off their log. Without the
    /// bit every area log carries it. Batch 11 step 11 removes the bit.
    /// </summary>
    AreaLogPrefixOnlyInSomeHandlers = 1L << 33,
}

/// <summary>The quirk sets the request builders give (batch 11 steps 7, 8 and 9).</summary>
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

    /// <summary>
    /// Today's NPC single-target path (TryNPCPerformSpellCast through the cast core with the AI decider, batch 11 step 8):
    /// the UI path's bits except the two that describe only the PC side (the combat-end check on a target's death,
    /// CORE-039, and the targeting highlight's range, SPL-017), plus the NPC path's own bits. It differs from
    /// <see cref="UiPath"/> only by bits that name a PC/NPC parity issue (Tests.Magic.CastCoreStageOrderTests checks the
    /// list).
    /// </summary>
    public const LegacyCastQuirk AiPath =
        (UiPath & ~(LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath | LegacyCastQuirk.RangeHighlightUsesCharacterLevel))
        | LegacyCastQuirk.NpcOwnLegalityChecks
        | LegacyCastQuirk.NpcRangeUsesCasterLevel
        | LegacyCastQuirk.NpcRefusesArea
        | LegacyCastQuirk.NpcNullMetamagic
        | LegacyCastQuirk.NpcNoComponentCheck
        | LegacyCastQuirk.NpcNoConcentrationOnCastCheck
        | LegacyCastQuirk.NpcNoConcentrationTracking
        | LegacyCastQuirk.NpcInvisibilityBreakBeforeCounterspell
        | LegacyCastQuirk.NpcSpendsBeforeFailureChecks
        | LegacyCastQuirk.NpcBlinkLabelsAndSilentPass
        | LegacyCastQuirk.NpcResultLogBeforeTargetDeath
        | LegacyCastQuirk.NpcCombatEndCheckedByCaller
        | LegacyCastQuirk.NpcNoMirrorImageSwap;

    /// <summary>
    /// Today's PC area path (PerformAoESpellCast through the cast core with the UI decider and the area resolve variant,
    /// batch 11 step 9): the UI path's bits that an area cast exhibits (the cost and provocation bits, the
    /// casting-while-concentrating step before the provocation, the scroll's spell failure, and the generic loop's
    /// forgone saves and SpellCaster deafness roll), without the single-target ones (the combat-end check on a target's
    /// death, the globe after the cast, the entangled formula, the targeting highlight's range, the Sound Burst and
    /// Animate Rope gates, the held-charge delivery), plus the area path's own bits. Tests.Magic.CastCoreStageOrderTests
    /// checks the list.
    /// </summary>
    public const LegacyCastQuirk UiAreaPath =
        (UiPath & ~(LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath
                    | LegacyCastQuirk.GlobeAfterCast
                    | LegacyCastQuirk.EntangledUsesCasterLevelFormula
                    | LegacyCastQuirk.RangeHighlightUsesCharacterLevel
                    | LegacyCastQuirk.SoundBurstStunIgnoresSuccess
                    | LegacyCastQuirk.AnimateRopeIgnoresSuccess
                    | LegacyCastQuirk.HeldChargeDeliveryIsFree))
        | LegacyCastQuirk.AreaSkipsEntangled
        | LegacyCastQuirk.AreaSkipsBlink
        | LegacyCastQuirk.AreaSkipsRing
        | LegacyCastQuirk.AreaSkipsPouch
        | LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers
        | LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers;
}
