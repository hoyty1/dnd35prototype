using System;

/// <summary>
/// Behaviours of today's cast paths that the staged cast core keeps on purpose until a later batch 11 step fixes them
/// (docs/designs/cast_core_batch11.md, "Migration rule"). Each bit names one known deviation and cites its issue. Request
/// builders set the bits (<see cref="SpellCastRequest.ForUi"/> and <see cref="SpellCastRequest.ForUiPrebuilt"/> set
/// <see cref="LegacyCastQuirkSets.UiPath"/>, the PC single-target path as it is today,
/// <see cref="SpellCastRequest.ForUiArea"/> sets <see cref="LegacyCastQuirkSets.UiAreaPath"/>, the PC area path,
/// <see cref="SpellCastRequest.ForUiSummon"/>, <see cref="SpellCastRequest.ForUiGrease"/> and
/// <see cref="SpellCastRequest.ForUiHoldCharge"/> set <see cref="LegacyCastQuirkSets.UiSummonPath"/>,
/// <see cref="LegacyCastQuirkSets.UiGreasePath"/> and <see cref="LegacyCastQuirkSets.UiHoldPath"/>, and
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

    // Bit 1 is unused: GlobeAfterCast (SPL-141) was removed by batch 11 step 11 (raw-failed-cast-gating), which tests the
    // globe before SpellCaster.Cast on the single-target path and before any area handler on the area path.

    // Bit 2 is unused: EntangledUsesCasterLevelFormula (SPL-145) was removed by batch 11 step 13
    // (raw-distraction-failure-stages); an entangled caster makes a Concentration check, DC 15 + the spell's level, for
    // any spell (PHB p.308, p.70; delegated RAW reading 2026-10-14).

    // Bit 3 is unused: QuickenedProvokes (SPL-143) was removed by batch 11 step 12 (raw-action-cost-provocation); a
    // quickened cast does not provoke (PHB p.98).

    /// <summary>
    /// A cast whose slot spend fails (or whose caster has no SpellcastingComponent) after the action was spent clears
    /// the resource snapshot, so the action is lost (SPL-147; batch 11 step 14). Without the bit the core restores it.
    /// </summary>
    SlotFailureClearsSnapshot = 1 << 4,

    // Bit 5 is unused: ConcentrationGateBeforeProvocation (SPL-152) was removed by batch 11 step 12; the
    // casting-while-concentrating step runs once the provocation lets the cast proceed.

    /// <summary>
    /// The PC range highlight (ShowSpellTargets) measures range by character level, not caster level (SPL-017; batch 11
    /// step 14). Not read by a stage yet: the range test is still the targeting highlight's; the Legality stage takes it
    /// over with CasterLevelRules.
    /// </summary>
    RangeHighlightUsesCharacterLevel = 1 << 6,

    // Bits 7 and 8 are unused: SoundBurstStunIgnoresSuccess and AnimateRopeIgnoresSuccess (SPL-142) were removed by batch
    // 11 step 11; a failed cast lands no Sound Burst stun (gate Success) and no Animate Rope entanglement (the handler
    // tests the failed result itself).

    /// <summary>
    /// The caster and every ally that is not charmed or confused forgo their save against every spell, harmful ones
    /// included (ShouldForceTargetToAcceptSave; SPL-149, PHB p.177; batch 11 step 18). Required whenever it makes the
    /// target forgo a save.
    /// </summary>
    AlliesForgoEverySave = 1 << 9,

    // Bit 10 is unused: DeafenedRolledInSpellCaster (SPL-150) was removed by batch 11 step 13
    // (raw-distraction-failure-stages); the deafened 20% verbal failure is rolled once per casting in the Failure stage
    // (CastComponentRules.TryDeafenedFailure, PHB p.307).

    // Bit 11 is unused: ScrollSkipsArcaneFailure (SPL-116) was removed by batch 11 step 13
    // (raw-distraction-failure-stages); a scroll cast rolls arcane spell failure for a spell with a somatic component (DMG
    // p.238, PHB p.122).

    /// <summary>
    /// Delivering a held touch charge costs no action in any round (SPL-156). RAW: the touch in the round of casting
    /// comes with the casting; after that round a delivery to a foe is a melee touch attack made instead of a regular
    /// attack, so it takes the attack action or one attack of a full attack (delegated RAW reading 2026-10-14), and
    /// touching one friend is a standard action, up to six friends a full-round action (PHB p.141, Holding the Charge).
    /// The core's Cost stage (batch 11 plan) removes the bit. Required for a held-charge delivery.
    /// </summary>
    HeldChargeDeliveryIsFree = 1 << 12,

    // Bits 13 and 14 are unused: QuickenBeforeSpontaneousFullRound (SPL-144) and SpontaneousFullRoundByCasterClass
    // (SPL-159) were removed by batch 11 step 12; the Legality stage refuses Quicken Spell on a spontaneous cast (PHB
    // p.98) and the Cost stage decides the full round of a metamagic cast by the cast's source (PHB p.88; CastActionRules).

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

    // Bit 23 is unused: NpcSpendsBeforeFailureChecks (SPL-160) was removed by batch 11 step 13
    // (raw-distraction-failure-stages); every caster spends its slot after the failure checks (a failed spell spends it in
    // the Failure stage).

    // Bit 24 is unused: NpcBlinkLabelsAndSilentPass (SPL-160) was removed by batch 11 step 13
    // (raw-distraction-failure-stages); both Blink rolls carry one label and log for every caster
    // (SpellResolutionService).

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

    // Bits 28 and 29 are unused: AreaSkipsEntangled and AreaSkipsBlink (SPL-006) were removed by batch 11 step 13
    // (raw-distraction-failure-stages); an area cast makes the entangled check and the blinking caster's 20% (PHB p.308,
    // p.206).

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

    // The summon, Grease and held-charge paths' own behaviours (batch 11 step 10): set only by SpellCastRequest.ForUiSummon,
    // ForUiGrease and ForUiHoldCharge (LegacyCastQuirkSets.UiSummonPath, UiGreasePath and UiHoldPath), and the held charge's
    // delivery (UiPath). Each names the issue of what that path skips or does differently today.

    // Bit 34 is unused: SummonSkipsBlink (SPL-006) was removed by batch 11 step 13 (raw-distraction-failure-stages).

    /// <summary>
    /// A summon is never offered to a readied counterspell (PHB p.170; SPL-006): the Counterspell stage is skipped. Batch
    /// 11 step 15 (raw-counterspell-order-parity) removes the bit.
    /// </summary>
    SummonSkipsCounterspell = 1L << 35,

    /// <summary>
    /// A summon makes no spell component pouch test (PHB p.174 and p.130; SPL-006, SPL-114): the Legality stage's
    /// component test passes it (Summon Swarm has an M/DF component). Batch 11 step 15 removes the bit.
    /// </summary>
    SummonSkipsPouch = 1L << 36,

    // Bit 37 is unused: SummonItemCastCommitsCastAction (SPL-163) was removed by batch 11 step 12; a summon from a
    // scroll or wand charges only the item's action, as every item cast does.

    // Bit 38 is unused: GreaseSkipsBlink (SPL-006) was removed by batch 11 step 13 (raw-distraction-failure-stages).

    /// <summary>
    /// Grease is never offered to a readied counterspell (PHB p.170; SPL-006): the Counterspell stage is skipped. Batch 11
    /// step 15 removes the bit.
    /// </summary>
    GreaseSkipsCounterspell = 1L << 39,

    /// <summary>
    /// Grease makes no spell component pouch test for its material component (PHB p.237, p.174 and p.130; SPL-006,
    /// SPL-114): the Legality stage's component test passes it. Batch 11 step 15 removes the bit.
    /// </summary>
    GreaseSkipsPouch = 1L << 40,

    /// <summary>
    /// Grease never ends the caster's invisibility (PHB p.245; SPL-006), even when its area takes in a foe: the
    /// InvisibilityBreak stage is skipped. Whether greasing an object a foe holds or wears is an attack on the foe is a
    /// reading the step that removes the bit records (batch 11 step 15, with the Ring of Counterspells reading of
    /// AreaSkipsRing).
    /// </summary>
    GreaseSkipsInvisibilityBreak = 1L << 41,

    // Bit 42 is unused: GreaseConcentrationCheckedTwice (SPL-152) was removed by batch 11 step 12; Grease runs the
    // casting-while-concentrating step once, as every cast.

    // Bit 43 is unused. A hold needs no Resilient Sphere bit: holding a charge inside a sphere is legal (PHB p.258:
    // nothing crosses the sphere until the charge is delivered; delegated RAW reading 2026-10-14), and the delivery's
    // boundary test belongs to CMB-092.

    /// <summary>
    /// Holding a touch charge skips the grapple legality test (CanBeginSpellcastWhileGrappledOrPinned, PHB p.156;
    /// SPL-146): without the bit the Legality stage runs it. Batch 11 step 15 removes the bit.
    /// </summary>
    HoldSkipsGrappleLegality = 1L << 44,

    // Bits 45 and 46 are unused: HoldSkipsEntangled and HoldSkipsBlink (SPL-146) were removed by batch 11 step 13
    // (raw-distraction-failure-stages); holding a charge is casting the spell (PHB p.141), so it makes the entangled check
    // and the blinking caster's 20%.

    /// <summary>
    /// Holding a touch charge makes no spell component pouch test (PHB p.174 and p.130; SPL-146, SPL-114): the Legality
    /// stage's component test passes it. Batch 11 step 15 removes the bit.
    /// </summary>
    HoldSkipsPouch = 1L << 47,

    /// <summary>
    /// Holding a touch charge is never offered to a readied counterspell (PHB p.170; SPL-146): the Counterspell stage is
    /// skipped. The charge is the spell already cast (PHB p.141), so it is countered when it is cast, at the hold. Batch
    /// 11 step 15 removes the bit.
    /// </summary>
    HoldSkipsCounterspell = 1L << 48,

    /// <summary>
    /// Holding a touch charge asks none of the caster's choices (the Resist Energy and Protection from Energy types, the
    /// Magic Weapon, Keen Edge and Greater Magic Weapon pickers; SPL-146): the Legality stage does not call the decider's
    /// FillChoices, so the delivery's cast asks them. The choices belong to the casting (PHB p.141: the charge is the spell
    /// already cast); batch 11 step 15 removes the bit.
    /// </summary>
    HoldSkipsChoices = 1L << 49,

    /// <summary>
    /// Holding the charge of a cleric's converted cure or inflict spell ignores the conversion: the Cost stage treats the
    /// cast as a plain slot cast, so the spend looks for a prepared copy of the cure or inflict spell (SPL-154, SPL-147).
    /// Batch 11 step 14 (raw-resources-caster-level) removes the bit.
    /// </summary>
    HoldIgnoresConversion = 1L << 50,

    /// <summary>
    /// Delivering a held touch charge is never offered to a readied counterspell, and while the hold is not either
    /// (<see cref="HoldSkipsCounterspell"/>) a held charge can never be countered (SPL-146). Delivering is no casting
    /// (PHB p.141), so the delivery's skip is the rule; the Counterspell stage requires the bit until batch 11 step 15
    /// counters the charge at the hold and removes both bits. Required for a held-charge delivery.
    /// </summary>
    DeliverySkipsCounterspell = 1L << 51,

    // Bit 52 is unused: LostCastShowsActionMenu (SPL-006) was removed by batch 11 step 13
    // (raw-distraction-failure-stages); a summon or Grease lost to a distraction or a spell failure pauses like any lost
    // cast.

    /// <summary>
    /// A resolved summon, Grease or held charge runs no combat-end check (EvaluateCombatEnd), so an end it causes is
    /// caught at the next ShowActionChoices or turn boundary (CORE-039). Required: the Summon, Grease and HoldCharge
    /// resolves have no combat-end stage yet; batch 11 step 11 (raw-failed-cast-gating) gives them one and removes the bit.
    /// </summary>
    EffectResolveSkipsCombatEndCheck = 1L << 53,
}

/// <summary>The quirk sets the request builders give (batch 11 steps 7, 8, 9 and 10).</summary>
public static class LegacyCastQuirkSets
{
    /// <summary>Today's PC single-target path (PerformSpellCast through the cast core with the UI decider).</summary>
    public const LegacyCastQuirk UiPath =
        LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath
        | LegacyCastQuirk.SlotFailureClearsSnapshot
        | LegacyCastQuirk.RangeHighlightUsesCharacterLevel
        | LegacyCastQuirk.AlliesForgoEverySave
        | LegacyCastQuirk.HeldChargeDeliveryIsFree
        | LegacyCastQuirk.DeliverySkipsCounterspell;

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
        | LegacyCastQuirk.NpcResultLogBeforeTargetDeath
        | LegacyCastQuirk.NpcCombatEndCheckedByCaller
        | LegacyCastQuirk.NpcNoMirrorImageSwap;

    /// <summary>
    /// Today's PC area path (PerformAoESpellCast through the cast core with the UI decider and the area resolve variant,
    /// batch 11 step 9): the UI path's bits that an area cast exhibits (the snapshot cleared after a failed spend and the
    /// generic loop's forgone saves), without the single-target ones (the combat-end check on a target's death, the
    /// targeting highlight's range, the held-charge delivery), plus the area path's own bits.
    /// Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk UiAreaPath =
        (UiPath & ~(LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath
                    | LegacyCastQuirk.RangeHighlightUsesCharacterLevel
                    | LegacyCastQuirk.HeldChargeDeliveryIsFree
                    | LegacyCastQuirk.DeliverySkipsCounterspell))
        | LegacyCastQuirk.AreaSkipsRing
        | LegacyCastQuirk.AreaSkipsPouch
        | LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers
        | LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers;

    /// <summary>
    /// The UI path's bits that every summon, Grease and hold shows (batch 11 step 10): the snapshot cleared after a failed
    /// spend and the missing combat-end check (the cost, provocation and concentration-order bits went in batch 11 step 12).
    /// </summary>
    private const LegacyCastQuirk UiEffectPathShared =
        LegacyCastQuirk.SlotFailureClearsSnapshot
        | LegacyCastQuirk.EffectResolveSkipsCombatEndCheck;

    /// <summary>
    /// Today's PC summon path (PerformSummonMonsterCast and PerformSummonSwarmCast through the cast core's Summon resolve,
    /// batch 11 step 10; formerly TryConsumePendingSpellCast): the shared bits, the targeting highlight's range (the
    /// placement range is by character level too), plus the summon path's own bits. Tests.Magic.CastCoreStageOrderTests
    /// checks the list.
    /// </summary>
    public const LegacyCastQuirk UiSummonPath =
        UiEffectPathShared
        | LegacyCastQuirk.RangeHighlightUsesCharacterLevel
        | LegacyCastQuirk.SummonSkipsCounterspell
        | LegacyCastQuirk.SummonSkipsPouch;

    /// <summary>
    /// Today's PC Grease path (the three Grease casts, now one request through the cast core's Grease resolve, batch 11
    /// step 10): the shared bits plus the Grease path's own bits. Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk UiGreasePath =
        UiEffectPathShared
        | LegacyCastQuirk.GreaseSkipsCounterspell
        | LegacyCastQuirk.GreaseSkipsPouch
        | LegacyCastQuirk.GreaseSkipsInvisibilityBreak;

    /// <summary>
    /// Today's PC hold of a touch charge (Discharge Later through the cast core's HoldCharge resolve, batch 11 step 10;
    /// formerly HoldPendingMeleeTouchCharge): the shared bits plus the hold path's own. Tests.Magic.CastCoreStageOrderTests
    /// checks the list.
    /// </summary>
    public const LegacyCastQuirk UiHoldPath =
        UiEffectPathShared
        | LegacyCastQuirk.HoldSkipsGrappleLegality
        | LegacyCastQuirk.HoldSkipsPouch
        | LegacyCastQuirk.HoldSkipsCounterspell
        | LegacyCastQuirk.HoldSkipsChoices
        | LegacyCastQuirk.HoldIgnoresConversion;
}
