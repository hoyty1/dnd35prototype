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
/// <see cref="SpellCastRequest.ForAI"/> sets <see cref="LegacyCastQuirkSets.AiPath"/>, the NPC one, and
/// <see cref="SpellCastRequest.ForAIArea"/> and <see cref="SpellCastRequest.ForAIGrease"/> the NPC area and Grease ones,
/// <see cref="LegacyCastQuirkSets.AiAreaPath"/> and <see cref="LegacyCastQuirkSets.AiGreasePath"/>); only the cast core's stage
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

    // Bit 4 is unused: SlotFailureClearsSnapshot (SPL-147) was removed by batch 11 step 14 (raw-resources-caster-level);
    // a cast whose spend fails after the action was spent restores the resource snapshot, so it costs nothing.

    // Bit 5 is unused: ConcentrationGateBeforeProvocation (SPL-152) was removed by batch 11 step 12; the
    // casting-while-concentrating step runs once the provocation lets the cast proceed.

    // Bit 6 is unused: RangeHighlightUsesCharacterLevel (SPL-017) was removed by batch 11 step 14
    // (raw-resources-caster-level); the PC targeting highlight and the Legality stage's range test both read
    // CasterLevelRules.

    // Bits 7 and 8 are unused: SoundBurstStunIgnoresSuccess and AnimateRopeIgnoresSuccess (SPL-142) were removed by batch
    // 11 step 11; a failed cast lands no Sound Burst stun (gate Success) and no Animate Rope entanglement (the handler
    // tests the failed result itself).

    // Bit 9 is unused: AlliesForgoEverySave (SPL-149) was removed by batch 11 step 18 (raw-saves-and-sr); a target's own
    // decider gives up its save only against a harmless or beneficial spell of its own side (PHB p.177;
    // SpellSaveRules.IsHarmlessOrBeneficialTo), on the single-target path, the generic area loop and the area handlers.

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
    /// to the spell menu. Moving these tests into CheckLegality for every caster and removing the bit is batch 11
    /// step 22b (SPL-160): step 22 did only its AI part, so the AI now ranks only casts these tests accept
    /// (GameManager.CheckLegalityForAI runs them through CheckLegality).
    /// </summary>
    NpcOwnLegalityChecks = 1 << 15,

    // Bit 16 is unused: NpcRangeUsesCasterLevel (SPL-017) was removed by batch 11 step 14 (raw-resources-caster-level);
    // every single-target cast meets one range test (CasterLevelRules.InRange: the cast's caster level, to the target's
    // nearest square).

    // Bit 17 is unused: NpcRefusesArea (AI-001) was removed by batch 11 step 25 (npc-area-casting); the AI aims an area
    // spell (AreaAimSearch) and casts it through the area resolve (SpellCastRequest.ForAIArea, ForAIGrease), and a
    // single-target request of an Area spell is refused for every caster ("area-needs-aim": an area spell is aimed at a
    // point, PHB p.175; Tests.Magic.SpellRoutingGoldenTests mirrors this data refusal).

    // Bit 18 is unused: NpcNullMetamagic (AI-001) was removed by batch 11 step 21 (metamagic-legality-and-slots); an NPC
    // cast carries its prepared metamagic slot's metamagic (SpellCastRequest.ForAI) or a Scripted step's, and meets the
    // same metamagic rules (MetamagicSystem). The AI still chooses no metamagic of its own (AI-001, batch 11 step 28).

    // Bit 19 is unused: NpcNoComponentCheck (SPL-114) was removed by batch 11 step 17 (raw-components-all-casters,
    // decision D3); every caster makes the one component test (SpellComponentRules) and NPC casters carry the pouch and
    // divine focus their class calls for (SpellComponentKit).

    // Bits 20 and 21 are unused: NpcNoConcentrationOnCastCheck and NpcNoConcentrationTracking (SPL-114) were removed by
    // batch 11 step 15 (raw-counterspell-order-parity); every caster runs the casting-while-concentrating step
    // (HandleConcentrationOnCasting) and a Concentration-duration effect starts its caster's concentration
    // (BeginConcentrationTracking), so damage can break an NPC's Silent Image as it breaks a PC's.

    // Bit 22 is unused: NpcInvisibilityBreakBeforeCounterspell (SPL-118) was removed by batch 11 step 15; every cast runs
    // decision D4's order: the readied counterspell (a countered spell leaves its caster invisible), then the hostile-cast
    // invisibility break, then the Ring of Counterspells (a spell the ring absorbs still ends the invisibility).

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

    // Bit 27 is unused: NpcNoMirrorImageSwap (SPL-117) was removed by batch 11 step 26 (raw-mirror-image, decision D7):
    // Mirror Image picks a random figure for every attack and spell against the caster, for every caster and attacker
    // (MirrorImageRules), and the end-of-turn swap offered only to controllable casters is gone.

    // The PC area path's own behaviours (batch 11 step 9): set only by SpellCastRequest.ForUiArea
    // (LegacyCastQuirkSets.UiAreaPath). Each names the issue of what an area cast skips or does differently today.

    // Bits 28 and 29 are unused: AreaSkipsEntangled and AreaSkipsBlink (SPL-006) were removed by batch 11 step 13
    // (raw-distraction-failure-stages); an area cast makes the entangled check and the blinking caster's 20% (PHB p.308,
    // p.206).

    // Bits 30 and 31 are unused: AreaSkipsRing and AreaSkipsPouch (SPL-006) were removed by batch 11 step 15. An area
    // cast makes the spell component pouch test like any cast, and the RingCounterspell stage runs for it but tests only
    // a single-target cast's target: the ring counters a spell "cast upon the wearer" (DMG p.230), and an area spell is
    // cast at an area, not on the creatures in it (PHB p.175; delegated RAW reading 2026-10-14).

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

    // The summon, Grease and held-charge paths' own behaviours (batch 11 step 10) were set only by
    // SpellCastRequest.ForUiSummon, ForUiGrease and ForUiHoldCharge (LegacyCastQuirkSets.UiSummonPath, UiGreasePath and
    // UiHoldPath), and the held charge's delivery (UiPath); batch 11 steps 13 and 15 removed them all.

    // Bit 34 is unused: SummonSkipsBlink (SPL-006) was removed by batch 11 step 13 (raw-distraction-failure-stages).

    // Bits 35 and 36 are unused: SummonSkipsCounterspell and SummonSkipsPouch (SPL-006) were removed by batch 11 step 15;
    // a summon meets a readied counterspell and the pouch test like any cast.

    // Bit 37 is unused: SummonItemCastCommitsCastAction (SPL-163) was removed by batch 11 step 12; a summon from a
    // scroll or wand charges only the item's action, as every item cast does.

    // Bit 38 is unused: GreaseSkipsBlink (SPL-006) was removed by batch 11 step 13 (raw-distraction-failure-stages).

    // Bits 39, 40 and 41 are unused: GreaseSkipsCounterspell, GreaseSkipsPouch and GreaseSkipsInvisibilityBreak (SPL-006)
    // were removed by batch 11 step 15. Grease meets a readied counterspell and the pouch test, and ends its caster's
    // invisibility when its square takes in a foe or when it greases an object a foe holds or wears (PHB p.245 spares
    // only actions directed at unattended objects; delegated RAW reading 2026-10-14).

    // Bit 42 is unused: GreaseConcentrationCheckedTwice (SPL-152) was removed by batch 11 step 12; Grease runs the
    // casting-while-concentrating step once, as every cast.

    // Bit 43 is unused. A hold needs no Resilient Sphere bit: holding a charge inside a sphere is legal (PHB p.258:
    // nothing crosses the sphere until the charge is delivered; delegated RAW reading 2026-10-14), and the delivery's
    // boundary test belongs to CMB-092.

    // Bit 44 is unused: HoldSkipsGrappleLegality (SPL-146) was removed by batch 11 step 15; holding a charge is casting
    // the spell (PHB p.141), so a grappled or pinned caster meets the grapple casting rule (PHB p.156) at the hold.

    // Bits 45 and 46 are unused: HoldSkipsEntangled and HoldSkipsBlink (SPL-146) were removed by batch 11 step 13
    // (raw-distraction-failure-stages); holding a charge is casting the spell (PHB p.141), so it makes the entangled check
    // and the blinking caster's 20%.

    // Bits 47, 48 and 49 are unused: HoldSkipsPouch, HoldSkipsCounterspell and HoldSkipsChoices (SPL-146) were removed
    // by batch 11 step 15. A hold makes the pouch test, meets a readied counterspell (the charge is the spell already cast,
    // PHB p.141, so it is countered at the hold) and asks the caster's choices for the spell (the energy type), which the
    // held charge keeps for its delivery (delegated RAW reading 2026-10-14).

    // Bit 50 is unused: HoldIgnoresConversion (SPL-154) was removed by batch 11 step 14 (raw-resources-caster-level); a
    // hold of a cleric's converted cure or inflict spell spends the sacrificed spell.

    // Bit 51 is unused: DeliverySkipsCounterspell (SPL-146) was removed by batch 11 step 15; delivering a held charge is
    // no casting (PHB p.141), so the Counterspell stage passes it by rule, and the charge met the counterspell at its hold.

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

/// <summary>The quirk sets the request builders give (batch 11 steps 7, 8, 9, 10 and 25; trimmed by steps 11 to 18 and 25).</summary>
public static class LegacyCastQuirkSets
{
    /// <summary>Today's PC single-target path (PerformSpellCast through the cast core with the UI decider).</summary>
    public const LegacyCastQuirk UiPath =
        LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath
        | LegacyCastQuirk.HeldChargeDeliveryIsFree;

    /// <summary>
    /// Today's NPC single-target path (TryNPCPerformSpellCast through the cast core with the AI decider, batch 11 step 8):
    /// the UI path's bits except the one that describes only the PC side (the combat-end check on a target's death,
    /// CORE-039), plus the NPC path's own bits. It differs from <see cref="UiPath"/> only by bits that name a PC/NPC
    /// parity issue (Tests.Magic.CastCoreStageOrderTests checks the list).
    /// </summary>
    public const LegacyCastQuirk AiPath =
        (UiPath & ~LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath)
        | LegacyCastQuirk.NpcOwnLegalityChecks
        | LegacyCastQuirk.NpcResultLogBeforeTargetDeath
        | LegacyCastQuirk.NpcCombatEndCheckedByCaller;

    /// <summary>
    /// Today's PC area path (PerformAoESpellCast through the cast core with the UI decider and the area resolve variant,
    /// batch 11 step 9): the UI path's bits that an area cast exhibits (none since batch 11 step 18 removed the generic
    /// loop's forgone saves), without the single-target ones (the combat-end check on a target's death, the held-charge
    /// delivery), plus the area path's own bits. Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk UiAreaPath =
        (UiPath & ~(LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath
                    | LegacyCastQuirk.HeldChargeDeliveryIsFree))
        | LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers
        | LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers;

    /// <summary>
    /// The NPC area path (batch 11 step 25, SpellCastRequest.ForAIArea: an area the AI aimed with AreaAimSearch, through
    /// the same area resolve as the PC's): the PC area path's bits plus the NPC path's own caster tests
    /// (<see cref="LegacyCastQuirk.NpcOwnLegalityChecks"/>, SPL-160: a standard action, a caster that can cast, a slot;
    /// refusals unlogged). The NPC wrapper runs its combat-end check after the cast as well, which finds the combat over
    /// when the area handler's own check ended it. Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk AiAreaPath =
        UiAreaPath
        | LegacyCastQuirk.NpcOwnLegalityChecks;

    /// <summary>
    /// The UI path's bits that every summon, Grease and hold shows (batch 11 step 10): the missing combat-end check (the
    /// cost, provocation and concentration-order bits went in batch 11 step 12, the snapshot cleared after a failed spend
    /// in step 14).
    /// </summary>
    private const LegacyCastQuirk UiEffectPathShared =
        LegacyCastQuirk.EffectResolveSkipsCombatEndCheck;

    /// <summary>
    /// Today's PC summon path (PerformSummonMonsterCast and PerformSummonSwarmCast through the cast core's Summon resolve,
    /// batch 11 step 10; formerly TryConsumePendingSpellCast): the shared bits only (the placement range is the cast's
    /// caster level's since batch 11 step 14; the counterspell and pouch skips went in batch 11 step 15).
    /// Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk UiSummonPath =
        UiEffectPathShared;

    /// <summary>
    /// Today's PC Grease path (the three Grease casts, now one request through the cast core's Grease resolve, batch 11
    /// step 10): the shared bits only (the counterspell, pouch and invisibility skips went in batch 11 step 15).
    /// Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk UiGreasePath =
        UiEffectPathShared;

    /// <summary>
    /// The NPC Grease path (batch 11 step 25, SpellCastRequest.ForAIGrease: Grease on a 10-ft. square the AI aimed): the PC
    /// Grease path's bits plus the NPC path's own caster tests (<see cref="LegacyCastQuirk.NpcOwnLegalityChecks"/>,
    /// SPL-160). Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk AiGreasePath =
        UiGreasePath
        | LegacyCastQuirk.NpcOwnLegalityChecks;

    /// <summary>
    /// Today's PC hold of a touch charge (Discharge Later through the cast core's HoldCharge resolve, batch 11 step 10;
    /// formerly HoldPendingMeleeTouchCharge): the shared bits only (the grapple, pouch, counterspell and choice skips went
    /// in batch 11 step 15). Tests.Magic.CastCoreStageOrderTests checks the list.
    /// </summary>
    public const LegacyCastQuirk UiHoldPath =
        UiEffectPathShared;
}
