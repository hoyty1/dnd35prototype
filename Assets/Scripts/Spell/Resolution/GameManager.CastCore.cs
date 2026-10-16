using System;
using System.Collections.Generic;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// The stages of the cast core (batch 11 step 7, docs/designs/cast_core_batch11.md), in the order a cast runs them
/// today, except that ConcentrationOnCast runs after Provocation, once the provocation lets the cast proceed (batch 11
/// step 12, SPL-152). The Globe stage runs before Resolve on the single-target and area paths (batch 11 step 11,
/// SPL-141). Since batch 11 step 15 every cast runs Counterspell, InvisibilityBreak, then RingCounterspell (decision D4,
/// SPL-118).
/// </summary>
public enum CastStage
{
    Legality,
    Cost,
    Distraction,
    Failure,
    Spend,
    ConcentrationOnCast,
    Provocation,
    Counterspell,
    InvisibilityBreak,
    RingCounterspell,
    MirrorImage,
    TargetBlink,
    Globe,
    Resolve,
    Handlers,
    Buff,
    PostCast,
    CombatEnd
}

/// <summary>How a cast ended (<see cref="CastOutcome"/>).</summary>
public enum CastOutcomeKind
{
    /// <summary>The spell was cast and resolved (on its target or, through Mirror Image, on a figment).</summary>
    Resolved,

    /// <summary>The cast was not legal and nothing was spent (target, choice, components).</summary>
    Refused,

    /// <summary>The caster backed out at the AoO prompt; the snapshot gave everything back.</summary>
    Cancelled,

    /// <summary>The spell was lost to a distraction: entangled or grappled Concentration, or the attacks of opportunity.</summary>
    Interrupted,

    /// <summary>The cast was spent but the spell failed (spell failure, counterspell, a blinking target) or could not be spent.</summary>
    Failed
}

/// <summary>The end of one run of the cast core: what happened, at which stage, why (a short code) and the result.</summary>
public sealed class CastOutcome
{
    // Reasons the Ui decider reads.
    public const string ReasonSlotNotSpent = "slot-not-spent";
    public const string ReasonNoSpellcasting = "no-spellcasting";

    public readonly CastOutcomeKind Kind;
    public readonly CastStage At;
    public readonly string Reason;
    public readonly SpellResult Result;

    /// <summary>Extra text for the reason (the missing component of a refusal), or null.</summary>
    public string Detail;

    /// <summary>True when the cast's combat-end check ended the combat.</summary>
    public bool CombatEnded;

    /// <summary>
    /// True when the UI goes straight back to the action menu after this cast, with no stat-panel refresh and no pause,
    /// although the cast counts as cast (<see cref="WasCast"/> is unchanged): a summon whose creature could not be
    /// spawned. A cast lost to a distraction or a spell failure pauses like any other (batch 11 step 13).
    /// </summary>
    public bool ShowsActionMenu;

    public CastOutcome(CastOutcomeKind kind, CastStage at, string reason, SpellResult result = null)
    {
        Kind = kind;
        At = at;
        Reason = reason;
        Result = result;
    }

    /// <summary>
    /// True when the cast took no turn time the UI should pause for: a refusal, a cancel, or a spend that failed (the
    /// old exits went straight back to the action menu).
    /// </summary>
    public bool BackToActionMenu
        => Kind == CastOutcomeKind.Refused || Kind == CastOutcomeKind.Cancelled
           || Reason == ReasonSlotNotSpent || Reason == ReasonNoSpellcasting;

    /// <summary>
    /// True when the cast counts as cast: it passed Legality and spent its slot, whatever came of the spell afterwards
    /// (resolved, lost to a distraction or the attacks of opportunity, failed, countered). False for a refusal, a cancel
    /// and a slot that could not be spent. TryNPCPerformSpellCast returns it, exactly as its old body returned true or false
    /// (GameManager.RunCastSync).
    /// </summary>
    public bool WasCast => !BackToActionMenu;

    public override string ToString() => $"{Kind}@{At}({Reason})";
}

/// <summary>
/// GameManager partial class: the staged cast core (batch 11 step 7, docs/designs/cast_core_batch11.md).
///
/// <see cref="ExecuteCast"/> runs one cast's <see cref="CastContext"/> through a fixed list of stages, one private
/// method per stage, each calling the next (continuation style: the decider may suspend the run at the choice dialogs
/// and the AoO prompt, and the run goes on from its callback). Every run ends in <see cref="FinishCast"/>, the single
/// exit: it shows the cast's result log once, ends the context (EndCast) and calls the decider's finish continuation,
/// which does the UI follow-up for a PC (GameManager.CastDeciders.cs). Stage methods never touch CombatUI except to log
/// through CombatLogHelper; they read the request and its context, and the legacy behaviours of today's paths only as
/// <see cref="LegacyCastQuirk"/> bits of the request (Tests.Magic.CastCoreStageOrderTests checks both).
///
/// Step 7 runs the PC single-target casts (PerformSpellCast) through the core with the UI decider, as a pure move of
/// PerformSpellCast's old body: every check, roll, log line and trace mark in today's order. Step 8 runs the NPC
/// single-target casts (TryNPCPerformSpellCast) through the same stages with the AI decider (<see cref="RunCastSync"/>),
/// as a pure move of the old NPC executor: its differences from the PC path are the request's NPC quirk bits
/// (LegacyCastQuirkSets.AiPath), which only the stages read. No stage asks who controls the caster; only the entry
/// points choose the decider. Step 9 runs the PC area casts (PerformAoESpellCast) through the same stages with the area
/// resolve variant (SpellCastRequest.ForUiArea, CastResolveKind.Area), as a pure move of PerformAoESpellCast's old
/// body: the Legality stage filters the area by line of effect, the shared stages spend and check the cast (the area
/// path's skips are the bits of LegacyCastQuirkSets.UiAreaPath), and after the invisibility break the area Resolve
/// stage (<see cref="CastStageResolveArea"/>) runs the area handler registry (GameManager.SpellHandlerRegistry.cs); every
/// area cast ends in <see cref="FinishAreaCast"/>, which shows the log once with its prefixes and runs the handler's
/// combat-end check before FinishCast. Step 10 runs the summon, Grease and held-charge casts through the same stages with
/// the Summon, Grease and HoldCharge resolve variants (SpellCastRequest.ForUiSummon, ForUiGrease, ForUiHoldCharge), as a
/// pure move of TryConsumePendingSpellCast, its five callers and HoldPendingMeleeTouchCharge: their skips are the bits of
/// LegacyCastQuirkSets.UiSummonPath, UiGreasePath and UiHoldPath, and the resolve stages
/// (<see cref="CastStageResolveSummon"/>, <see cref="CastStageResolveGrease"/>, <see cref="CastStageResolveHoldCharge"/>)
/// run the old resolution code.
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// Runs <paramref name="ctx"/> (an open cast, normally the top of the cast stack) through the stages. The cast must
    /// carry its caster, spell and target (Request.Aim.Target). <paramref name="done"/> is called once with the outcome,
    /// after the decider's own finish continuation; it is never called for a run the decider suspended (a dialog opened
    /// that re-enters the cast or ends it).
    /// </summary>
    internal void ExecuteCast(CastContext ctx, Action<CastOutcome> done)
    {
        if (ctx == null)
        {
            done?.Invoke(new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "no-cast"));
            return;
        }

        if (RejectCastReentry(ctx, nameof(ExecuteCast)))
            return;

        ctx.ResetRun();
        ctx.Done = done;
        CastStageLegality(ctx);
    }

    /// <summary>
    /// Runs <paramref name="req"/> through the cast core at once, for a decider that answers inline (the AI): opens its
    /// context on the cast stack, runs <see cref="ExecuteCast"/> and returns whether the cast counts as cast
    /// (<see cref="CastOutcome.WasCast"/>), the bool TryNPCPerformSpellCast has always returned. The run must finish
    /// before this returns; one that did not (a decider that suspended) is ended here and raises a cast-stack violation
    /// (ScenarioHooks.CastStackViolation, which the scenario harness turns into a failure), and so does a decider that is
    /// not synchronous. Such a run counts as cast only if it stopped after its slot was spent. An exception still ends the
    /// context. The GameManager's cancel snapshot (_pendingSpellcastSnapshot, which the Cost stage captures and the later
    /// stages clear) is put back as it was before the run: the old NPC executor never touched it, and a synchronous AI cast
    /// never cancels, so a nested AI cast cannot take the snapshot of a PC cast waiting at its AoO prompt.
    /// <paramref name="stageObserver"/> sees each stage (a test).
    /// </summary>
    internal bool RunCastSync(SpellCastRequest req, out CastOutcome outcome, Action<CastStage> stageObserver = null)
    {
        if (req == null)
        {
            outcome = new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "no-cast");
            return false;
        }

        if (req.Decider == null || !req.Decider.IsSynchronous)
        {
            ReportCastStackViolation("RunCastSync was given " + DescribeCast(req) + ", whose decider is not synchronous");
            outcome = new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "not-synchronous");
            return false;
        }

        CastResourceSnapshot outerSnapshot = PeekSpellcastResourceSnapshot();
        CastContext ctx = BeginCast(req);
        ctx.StageObserver = stageObserver;
        CastOutcome finished = null;
        try
        {
            ExecuteCast(ctx, o => finished = o);
        }
        finally
        {
            // A run that threw or did not finish must not stay on the cast stack (the old NPC executor's finally).
            if (finished == null && !ctx.Ended)
                EndCast(ctx);
            PutBackSpellcastResourceSnapshot(outerSnapshot);
        }

        if (finished == null)
        {
            ReportCastStackViolation("RunCastSync: the cast " + DescribeCast(req) + " did not finish synchronously (stopped in stage " + ctx.Stage + ")");
            // Counts as cast only once the slot is spent: after the Spend stage.
            bool slotSpent = ctx.Stage > CastStage.Spend;
            finished = new CastOutcome(slotSpent ? CastOutcomeKind.Failed : CastOutcomeKind.Refused, ctx.Stage, "not-synchronous");
        }

        outcome = finished;
        return finished.WasCast;
    }

    /// <summary>
    /// Whether <paramref name="req"/> may be cast as it stands: with <see cref="LegacyCastQuirk.NpcOwnLegalityChecks"/>
    /// the NPC path's own tests first (the standard action, a living target, the caster's spellcasting and a slot), the
    /// decider's capability (<see cref="CheckDeciderCapability"/>: a choice its decider cannot make, batch 11 step 25), the
    /// target test (IsValidTargetForSpell; a single-target request of an Area spell is refused, "area-needs-aim"), the
    /// range at the cast's caster level (CasterLevelRules, SPL-017), and
    /// the metamagic (MetamagicSystem, since batch 11 step 21; <see cref="CheckCastMetamagicLegality"/>), the casting
    /// action, Silence (a verbal component in a Silence, PHB p.279; <see cref="CheckCastSpeechLegality"/>), the
    /// component test (the pouch, a divine focus, a priced focus or material component, PHB p.174 and p.130;
    /// SpellComponentRules, one rule for every caster since batch 11 step 17; skipped for the F12 panel's Components
    /// bypass, scrolls and wands), and for a hold the grapple test (<see cref="CheckHoldChargeLegality"/>, since step 15). No side
    /// effect: the grapple refusal's log line is the Legality stage's, not this pre-check's. A refusal comes back with
    /// Kind Refused; a legal request with Kind Resolved and reason "ok". The caster's choices are not checked here (the
    /// decider makes them).
    /// </summary>
    internal CastOutcome CheckLegality(SpellCastRequest req)
    {
        CastOutcome refusal = CheckNpcCasterLegality(req) ?? CheckHoldChargeLegality(req) ?? CheckDeciderCapability(req)
                              ?? CheckCastTargetLegality(req) ?? CheckCastRangeLegality(req) ?? CheckCastMetamagicLegality(req) ?? CheckCastActionLegality(req)
                              ?? CheckCastSpeechLegality(req)
                              ?? CheckCastComponentLegality(req);
        return refusal ?? new CastOutcome(CastOutcomeKind.Resolved, CastStage.Legality, "ok");
    }

    /// <summary>
    /// The AI's legality filter (batch 11 step 22): <see cref="CheckLegality"/> on the request the NPC cast of
    /// <paramref name="spell"/> at <paramref name="target"/> would build (SpellCastRequest.ForAI), so the AI ranks only
    /// casts the cast core would let through (the NPC caster tests, IsValidTargetForSpell, the range to the target's
    /// footprint at the cast's caster level, the metamagic, the casting action, Silence and the components). The
    /// caster's choices are not tested here: the decider makes them in the cast (AiCastDecider.FillChoices). No side effect.
    /// </summary>
    internal CastOutcome CheckLegalityForAI(CharacterController caster, CharacterController target, SpellData spell)
        => CheckLegality(SpellCastRequest.ForAI(caster, target, spell));

    /// <summary>
    /// The AI's legality filter for an area it aimed (batch 11 step 25): <see cref="CheckLegality"/> on the request the
    /// NPC area cast of <paramref name="spell"/> at <paramref name="placement"/> would build
    /// (<see cref="BuildAiAreaRequest"/>: the decider's capability, the point of origin's range and line of effect, the
    /// NPC caster tests, the metamagic, the casting action, Silence and the components). No side effect.
    /// </summary>
    internal CastOutcome CheckAreaLegalityForAI(CharacterController caster, SpellData spell, AreaPlacement placement)
        => CheckLegality(BuildAiAreaRequest(caster, spell, placement, null));

    /// <summary>
    /// The request of an NPC area cast of <paramref name="spell"/> aimed at <paramref name="placement"/> (an aim of
    /// AreaAimSearch): an area request (SpellCastRequest.ForAIArea), or Grease's area form (ForAIGrease), with a copy of
    /// the placement's squares and the creatures in them that the spell's filter takes in, judged as the PC's area is
    /// (AoESystem.GetTargetsInArea, relative to the caster's side). The core's Legality stage filters both by line of
    /// effect from the placement's origin. Without a placement: the bare request the AI's search and caster tests start from.
    /// </summary>
    internal SpellCastRequest BuildAiAreaRequest(CharacterController caster, SpellData spell, AreaPlacement placement, MetamagicData metamagic)
    {
        bool grease = AreaAimSearch.IsGrease(spell);
        HashSet<Vector2Int> cells = placement != null ? new HashSet<Vector2Int>(placement.Cells) : null;
        List<CharacterController> targets = cells != null && Grid != null
            ? AoESystem.GetTargetsInArea(cells, caster, grease ? AoETargetFilter.All : spell.AoEFilter, Grid)
            : new List<CharacterController>();
        GridIntersection? origin = placement != null ? placement.Origin : null;
        return grease
            ? SpellCastRequest.ForAIGrease(caster, spell, cells, targets, origin, metamagic)
            : SpellCastRequest.ForAIArea(caster, spell, cells, targets, origin, metamagic);
    }

    /// <summary>
    /// The caster's own part of <see cref="CheckLegality"/> for an NPC cast of <paramref name="spell"/>, with no target,
    /// range or standard-action test, which the AI's planning reads (AIService.SelectSpell: the spell it would cast, also
    /// asked to size an approach before the caster is in range): the metamagic, the casting action, Silence and the
    /// components, the same tests in the same order as the cast core's Legality stage. Null when they pass.
    /// </summary>
    internal CastOutcome CheckCasterLegalityForAI(CharacterController caster, SpellData spell)
    {
        SpellCastRequest req = SpellCastRequest.ForAI(caster, null, spell);
        return CheckCastMetamagicLegality(req) ?? CheckCastActionLegality(req) ?? CheckCastSpeechLegality(req)
               ?? CheckCastComponentLegality(req);
    }

    /// <summary>
    /// The grapple test a cast meets in its targeting (BeginPendingSpellTargeting), which a hold meets here: holding the
    /// charge is casting the spell (PHB p.141), so a grappled or pinned caster can hold only a spell that the grapple
    /// allows (GrappleSpellcastRefusal: a casting time of 1 standard action or less, no somatic component, and no verbal
    /// one while pinned; PHB p.156; batch 11 step 15, SPL-146). Holding a charge inside a Resilient Sphere is legal: the
    /// cast and the held charge send nothing through the sphere (PHB p.258); only a delivery to a creature on the other
    /// side crosses it, which is the delivery's test (CMB-092) (delegated RAW reading 2026-10-14). Null for any other
    /// request, or when the hold passes; a refusal carries its log line in <see cref="CastOutcome.Detail"/>, which the
    /// Legality stage shows, so the pre-check has no side effect.
    /// </summary>
    private CastOutcome CheckHoldChargeLegality(SpellCastRequest req)
    {
        if (req == null || req.ResolveKind != CastResolveKind.HoldCharge || req.Caster == null || req.Spell == null)
            return null;
        string grappleRefusal = GrappleSpellcastRefusal(req.Caster, req.Spell);
        if (grappleRefusal != null)
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "grapple") { Detail = grappleRefusal };
        return null;
    }

    /// <summary>
    /// The NPC path's own caster tests, in its old order (<see cref="LegacyCastQuirk.NpcOwnLegalityChecks"/>, SPL-160):
    /// a standard action left, a living target, a caster that can cast spells, a slot for the spell. Null when the
    /// request does not carry the bit or passes. The standard-action test is skipped for a cast that costs a free or
    /// swift action (CastActionRules.CastingAction: a quickened spell, PHB p.98, now reachable for NPCs through prepared
    /// metamagic slots and Scripted steps, batch 11 step 21), as the PC side asks it; the shared casting-action test
    /// (<see cref="CheckCastActionLegality"/>) then holds it to the swift action and the one quickened spell a round.
    /// </summary>
    private static CastOutcome CheckNpcCasterLegality(SpellCastRequest req)
    {
        if (req == null || !req.HasQuirk(LegacyCastQuirk.NpcOwnLegalityChecks) || req.Caster == null || req.Spell == null)
            return null;

        CharacterController caster = req.Caster;
        CharacterController target = req.Aim != null ? req.Aim.Target : null;
        CastActionCost cost = CastActionRules.CastingAction(req.Spell, req.Metamagic, req.Source);
        bool needsStandard = cost != CastActionCost.Free && cost != CastActionCost.Swift;
        if (caster.Actions == null || (needsStandard && !caster.Actions.HasStandardAction))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "no-standard-action");
        if (target != null && target.Stats != null && target.Stats.IsDead)
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "target-dead");
        SpellcastingComponent spellComp = caster.Spellcasting;
        if (spellComp == null || !spellComp.CanCastSpells)
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "no-spellcasting");
        // A slot or spell per day for the spell with the cast's metamagic: the one the spend takes (batch 11 step 21).
        if (!spellComp.CanCastWithMetamagic(req.Spell, req.Metamagic))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "cannot-cast");
        return null;
    }

    /// <summary>
    /// The range test, one rule for every caster and path (batch 11 step 14, SPL-017): a single-target cast whose target
    /// lies beyond the spell's range at the cast's caster level (CasterLevelRules.InRange: a scroll's or wand's own
    /// caster level, else the casting class's, half for a paladin or ranger; at least 1 square; measured to the target's
    /// nearest square, as the PC targeting highlight measures it) is refused. Null when the request passes. (An area's
    /// point of origin meets its own range test, <see cref="CheckAreaOriginLegality"/>.)
    /// </summary>
    private static CastOutcome CheckCastRangeLegality(SpellCastRequest req)
    {
        if (req == null || req.Caster == null || req.Spell == null)
            return null;

        CharacterController target = req.Aim != null ? req.Aim.Target : null;
        if (req.ResolveKind == CastResolveKind.Single && target != null && req.Caster.Stats != null
            && !CasterLevelRules.InRange(req, target))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "out-of-range");
        return null;
    }

    /// <summary>
    /// The decider's capability (batch 11 step 25; ICastDecider.CanFill): a request whose choices its decider cannot make
    /// (the AI cannot place a wall's ends, grease a held object or worn armor, or place a summoned swarm yet) is refused,
    /// "choices-incomplete", before anything is spent. Not a rule fork: the same request from a decider that can fill it
    /// goes on by the same rules. Null when the decider can fill the request.
    /// </summary>
    private static CastOutcome CheckDeciderCapability(SpellCastRequest req)
    {
        if (req == null || req.Decider == null || req.Decider.CanFill(req))
            return null;
        return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "choices-incomplete");
    }

    /// <summary>
    /// A burst's or cube's point of origin, the grid intersection the aim recorded (<see cref="CastAim.AreaOrigin"/>), must
    /// be within the spell's range of the caster's space, counted intersection to intersection (PHB p.175;
    /// AoESystem.IsOriginWithinRange, the test the PC cursor makes), and in the caster's line of effect (PHB p.176: "You
    /// must have a clear line of effect to the point of origin of any spell you cast"; AoESystem.HasLineOfEffectToOrigin):
    /// "origin-out-of-range" or "origin-no-line-of-effect" otherwise (batch 11 step 24, delegated RAW reading 2026-10-14 in
    /// RULES_COVERAGE 4.1). A Grease area's 10-ft. square must have its anchor square (the south-west one, the square the
    /// PC clicks) within the spell's range at the cast's caster level, and its origin, the square's centre, in the
    /// caster's line of effect; a one-square effect (Flaming Sphere, Call Lightning) must have its square within range
    /// (AoESystem.IsWithinCastingRange, the measure the PC highlight and the AI's aim search use; batch 11 step 25 review).
    /// The same for every caster. Nothing to test for a cone or line (it starts at the caster), an area centred on the
    /// caster (no range, its origin on the caster's space) or an aim with no recorded origin (the walls).
    /// </summary>
    private static CastOutcome CheckAreaOriginLegality(SpellCastRequest req)
    {
        SpellData spell = req.Spell;
        GridIntersection? origin = req.Aim != null ? req.Aim.AreaOrigin : null;
        Vector2Int casterBase = req.Caster.GridPosition;
        int casterSize = req.Caster.GetCurrentSpaceSquares();
        if (req.ResolveKind == CastResolveKind.Area && !origin.HasValue && AoESystem.IsSingleSquarePlacement(spell))
        {
            int squareRange = spell.AoERangeSquares > 0 ? spell.AoERangeSquares : CasterLevelRules.RangeSquares(req);
            if (req.Aim != null && req.Aim.Cells != null)
                foreach (Vector2Int square in req.Aim.Cells)
                    if (!AoESystem.IsWithinCastingRange(casterBase, square, squareRange))
                        return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "origin-out-of-range");
            return null;
        }
        if (!origin.HasValue)
            return null;
        if (req.ResolveKind == CastResolveKind.Grease)
        {
            int greaseRange = Mathf.Max(1, CasterLevelRules.RangeSquares(req));
            var anchor = new Vector2Int(origin.Value.X - 1, origin.Value.Y - 1); // GridIntersection.NorthEastCornerOf(anchor)
            if (!AoESystem.IsWithinCastingRange(casterBase, anchor, greaseRange))
                return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "origin-out-of-range");
            return AoESystem.HasLineOfEffectToOrigin(casterBase, casterSize, origin.Value)
                ? null
                : new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "origin-no-line-of-effect");
        }
        if (!AoESystem.IsPlacedShape(spell.AoEShapeType) || AoESystem.IsSingleSquarePlacement(spell))
            return null;
        int range = spell.AoERangeSquares > 0 ? spell.AoERangeSquares : CasterLevelRules.RangeSquares(req);
        if (range <= 0)
            return null;
        if (!AoESystem.IsOriginWithinRange(casterBase, casterSize, origin.Value, range))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "origin-out-of-range");
        if (!AoESystem.HasLineOfEffectToOrigin(casterBase, casterSize, origin.Value))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "origin-no-line-of-effect");
        return null;
    }

    private CastOutcome CheckCastTargetLegality(SpellCastRequest req)
    {
        if (req == null || req.Spell == null || req.Caster == null)
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "no-cast");
        // Only a single-target cast has a target creature to test. An area cast's creatures are the ones in the aimed area;
        // its point of origin must be within the spell's range and the caster's line of effect (CheckAreaOriginLegality; the
        // area's own line of effect is filtered at the start of the stage; batch 11 step 24); a summon aims at a square
        // and Grease at an area or an object, both picked from the targeting highlight (PerformSummonMonsterCast,
        // PerformSummonSwarmCast and PerformGreaseCast never had a target test either); a hold has no target until its
        // delivery, which has its own cast and test.
        if (req.ResolveKind == CastResolveKind.Area
            || (req.ResolveKind == CastResolveKind.Grease && req.Choices != null && req.Choices.GreaseMode == GreaseCastMode.Area))
            return CheckAreaOriginLegality(req);
        if (req.ResolveKind != CastResolveKind.Single)
            return null;
        // An area spell is aimed at a point, not cast on a creature (PHB p.175): it reaches its creatures through an area
        // request (the PC's area targeting, SpellCastRequest.ForUiArea; the AI's aim search, ForAIArea), never a
        // single-target one, for every caster (batch 11 step 25). "Area spell" is the PC routing's own predicate
        // (AoESystem.RoutesToArea: an Area target type, or an area shape such as Prayer's burst centred on the caster,
        // PHB p.264), so a spell has one cast path (batch 11 step 25 review; Tests.Magic.SpellRoutingGoldenTests mirrors
        // this data refusal: keep them in step).
        if (AoESystem.RoutesToArea(req.Spell))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "area-needs-aim");
        if (!IsValidTargetForSpell(req.Caster, req.Aim != null ? req.Aim.Target : null, req.Spell))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "invalid-target");
        return null;
    }

    /// <summary>
    /// The metamagic's rules (MetamagicSystem.ValidateMetamagicApplication, batch 11 step 21, SPL-055), the same for every
    /// caster and path: no Quicken Spell on a spontaneous cast (PHB p.98 Special), only a feat the caster has (a feat that
    /// does not change the spell is not refused), a Heighten that raises the level (PHB p.95), at most 9th level. An NPC's
    /// prepared metamagic spell carries its slot's metamagic in the request (SpellCastRequest.ForAI). Null when the cast
    /// may go on.
    /// </summary>
    private static CastOutcome CheckCastMetamagicLegality(SpellCastRequest req)
    {
        if (req == null || req.Caster == null || req.Spell == null)
            return null;
        string refusal = MetamagicSystem.ValidateMetamagicApplication(req.Spell, req.Metamagic, req.Caster.Stats, req.Source);
        return refusal == null ? null : new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, refusal);
    }

    /// <summary>
    /// The casting action's rules (CastActionRules.ActionRefusal, batch 11 step 12), the same for every caster and path:
    /// one quickened spell per round (PHB p.98; Feather Fall counts, PHB p.174, p.229), and a full-round or swift casting
    /// action the caster still has (PHB p.174, p.88). Null when the cast may go on.
    /// </summary>
    private static CastOutcome CheckCastActionLegality(SpellCastRequest req)
    {
        if (req == null || req.Caster == null || req.Spell == null)
            return null;
        string refusal = CastActionRules.ActionRefusal(req.Caster, req.Spell, req.Metamagic, req.Source);
        return refusal == null ? null : new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, refusal);
    }

    /// <summary>
    /// Silence (PHB p.279; batch 11 step 13, SPL-092), the same for every caster and path: a caster under a Silence cannot
    /// cast a spell with a verbal component, unless Silent Spell removed it (PHB p.100), nor use a wand, whose trigger word
    /// must be spoken (DMG p.213; delegated RAW reading 2026-10-14). CastComponentRules.SilenceBlocks decides; a held-charge
    /// delivery is no casting. Null when the cast may go on.
    /// </summary>
    private static CastOutcome CheckCastSpeechLegality(SpellCastRequest req)
    {
        if (req == null || req.Caster == null || req.Spell == null)
            return null;
        return CastComponentRules.SilenceBlocks(req.Caster, req.Spell, req.Metamagic, req.Source)
            ? new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "silenced")
            : null;
    }

    /// <summary>
    /// The component test, one rule for every caster and path (batch 11 step 17, decision D3; SPL-114, SPL-020): the
    /// spell component pouch for a material component or focus with no price, the divine focus of a divine casting, a
    /// priced focus item and the priced material components (SpellComponentRules.Refusal; PHB p.140, p.174, p.130). A
    /// held charge's delivery is no casting (PHB p.141): its components were met at the hold. Scrolls and wands supply
    /// their spell's components (DMG p.238), and the F12 panel's Components bypass skips the test. Nothing is spent
    /// before it passes; the refusal carries the missing component in <see cref="CastOutcome.Detail"/>.
    /// </summary>
    private static CastOutcome CheckCastComponentLegality(SpellCastRequest req)
    {
        if (req == null || req.Caster == null || req.Spell == null)
            return null;
        string reason = SpellComponentRules.Refusal(req.Caster, req.Spell, req.Source, out string missing);
        if (reason == null)
            return null;
        return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, reason) { Detail = missing };
    }

    // ── Stages ──────────────────────────────────────────────────────────────────────────────────────────────────

    private void EnterCastStage(CastContext ctx, CastStage stage)
    {
        ctx.Stage = stage;
        ctx.StageObserver?.Invoke(stage);
    }

    /// <summary>
    /// Legality: the target, the range at the cast's caster level (every caster, SPL-017), then the caster's choices (the
    /// decider; the UI may suspend here at a dialog), then the components. Nothing is spent before the cast passes. With
    /// the NPC bits: the NPC path's own caster tests before the target and its unlogged target and range refusals
    /// (<see cref="LegacyCastQuirk.NpcOwnLegalityChecks"/>). Before the target test, the decider's capability (batch 11
    /// step 25): a choice the decider cannot make refuses the cast. Since batch 11 step 21 an
    /// NPC cast keeps its metamagic (a prepared metamagic slot's, SpellCastRequest.ForAI) and meets the same metamagic
    /// rules (MetamagicSystem).
    /// </summary>
    private void CastStageLegality(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Legality);
        SpellCastRequest req = ctx.Request;
        CharacterController caster = req.Caster;
        SpellData spell = req.Spell;

        // An area reaches only the squares and creatures its origin has line of effect to (PHB p.176): an area cast, and a
        // Grease cast on a 10-ft. square (batch 11 step 24, SPL-006).
        if (req.ResolveKind == CastResolveKind.Area
            || (req.ResolveKind == CastResolveKind.Grease && req.Choices != null && req.Choices.GreaseMode == GreaseCastMode.Area))
            FilterAreaAimByLineOfEffect(req);

        CastOutcome refusal = CheckNpcCasterLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason);
            FinishCast(ctx, refusal);
            return;
        }

        // A hold meets the grapple test a cast meets in its targeting (PHB p.141, p.156; SPL-146).
        refusal = CheckHoldChargeLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason);
            if (!string.IsNullOrEmpty(refusal.Detail))
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", refusal.Detail));
            FinishCast(ctx, refusal);
            return;
        }

        // A choice the cast's decider cannot make (batch 11 step 25; ICastDecider.CanFill): refused before anything is
        // spent. Only the AI decider refuses (a wall's ends, Grease on an object or armor, a swarm's square); it logs nothing.
        refusal = CheckDeciderCapability(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason);
            FinishCast(ctx, refusal);
            return;
        }

        refusal = CheckCastTargetLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason == "area-needs-aim" ? refusal.Reason : "invalid-target");
            if (spell != null && !req.HasQuirk(LegacyCastQuirk.NpcOwnLegalityChecks))
                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"{spell.Name} has invalid target (requires humanoid ally/enemy constraints)."));
            FinishCast(ctx, refusal);
            return;
        }

        // The range at the cast's caster level (SPL-017), the same for every caster; the PC's target comes from the
        // targeting highlight, which applies the same rule, so a PC cast meets this only through a stale target. The NPC
        // path's refusals log nothing, as its others.
        refusal = CheckCastRangeLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason);
            if (refusal.Reason == "out-of-range" && !req.HasQuirk(LegacyCastQuirk.NpcOwnLegalityChecks))
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{req.Aim.Target?.Stats?.CharacterName ?? "The target"} is out of range of {spell.Name}."));
            FinishCast(ctx, refusal);
            return;
        }

        // The metamagic (MetamagicSystem, batch 11 step 21): Quicken on a spontaneous cast, a feat the caster lacks,
        // a Heighten that raises nothing, an adjusted level above 9th. Then the casting action
        // (batch 11 step 12): a second quickened spell this round, a full-round or swift action the caster no longer has.
        // The NPC path's refusals log nothing, as its others.
        refusal = CheckCastMetamagicLegality(req) ?? CheckCastActionLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason);
            if (!req.HasQuirk(LegacyCastQuirk.NpcOwnLegalityChecks))
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", CastActionRules.DescribeRefusal(refusal.Reason, caster.Stats?.CharacterName, spell.Name, req.Metamagic)));
            FinishCast(ctx, refusal);
            return;
        }

        // Silence (PHB p.279): no verbal component, and no wand's trigger word, inside one (SPL-092). The NPC path's
        // refusals log nothing, as its others.
        refusal = CheckCastSpeechLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason);
            if (!req.HasQuirk(LegacyCastQuirk.NpcOwnLegalityChecks))
                CombatUI?.ShowCombatLog(CombatLogHelper.Failure("🔇", CastComponentRules.DescribeSilenceRefusal(
                    caster.Stats?.CharacterName, spell.Name, CastComponentRules.IsSpellTrigger(req.Source))));
            FinishCast(ctx, refusal);
            return;
        }

        // The caster's choices, a hold's included (batch 11 step 15, SPL-146): the energy type of a held Resist Energy is
        // chosen at the hold and kept with the charge; a choice that picks the target (the weapon Magic Weapon touches)
        // waits for the delivery, which has the target (delegated RAW reading 2026-10-14).
        CastChoiceFill fill = req.Decider.FillChoices(this, ctx);
        if (fill == CastChoiceFill.Suspended)
            return;
        if (fill == CastChoiceFill.Refused)
        {
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "choice-refused"));
            return;
        }

        // The components (batch 11 step 17, decision D3): the same test and log line for every caster.
        refusal = CheckCastComponentLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, refusal.Reason);
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", SpellComponentRules.DescribeRefusal(caster.Stats?.CharacterName, spell.Name, refusal.Reason, refusal.Detail)));
            FinishCast(ctx, refusal);
            return;
        }

        CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, "ok");
        CastStageCost(ctx);
    }

    /// <summary>
    /// Cost: the snapshot for a cancel, then the action (none for a scroll or wand, which charge theirs when spent; none
    /// for a held-charge delivery; the quickened spell of the round; full round for spontaneous metamagic; else
    /// standard), the scenario hook for a cast, the spellcasting component, the spend's parameters and the log prefix.
    /// </summary>
    private void CastStageCost(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Cost);
        SpellCastRequest req = ctx.Request;
        CharacterController caster = req.Caster;
        CharacterController target = req.Aim.Target;
        SpellData spell = req.Spell;
        MetamagicData metamagic = req.Metamagic;

        CaptureSpellcastResourceSnapshot(caster, req.Source);

        CastSourceKind sourceKind = req.Source.Kind;
        bool isDeliveringHeldCharge = sourceKind == CastSourceKind.HeldChargeDelivery;
        bool isScrollCast = sourceKind == CastSourceKind.Scroll;
        bool isWandCast = sourceKind == CastSourceKind.Wand;
        ctx.IsHeldChargeDelivery = isDeliveringHeldCharge;

        // Quickened applies when CASTING the spell, not when delivering a previously held charge. A quickened cast and a
        // spell whose casting time is a free action (Feather Fall) are the caster's one quickened spell of the round and
        // provoke no attack of opportunity (PHB p.98, p.174, p.229; CastActionRules).
        bool isQuickened = CastActionRules.IsQuickened(metamagic, req.Source);
        ctx.IsQuickened = isQuickened;
        ctx.CountsAsQuickenedSpell = CastActionRules.CountsAsQuickenedSpell(spell, metamagic, req.Source);

        // One action cost from the cast's source and the spell's casting time (CastActionRules.CastingAction, batch 11
        // step 12): the item's own action for a scroll or wand (SPL-163), a free action for a quickened spell or a
        // free-action spell, a full round for a 1-round casting time (PHB p.174, SPL-162) or a spontaneous metamagic
        // cast (PHB p.88, by the source, SPL-159), else a standard action. Legality has already refused a full-round or
        // swift cost the caster cannot pay. A casting's action with its metamagic is MetamagicSystem's rule (batch 11 step
        // 21, SPL-055), the one CastActionRules.CastingAction gives the PC menu and the AI.
        CastActionCost actionCost = isScrollCast || isWandCast || isDeliveringHeldCharge
            ? CastActionCost.ItemOrDelivery
            : MetamagicSystem.GetMetamagicCastingAction(spell, metamagic, req.Source);
        if (isScrollCast || isWandCast)
        {
            // Scroll/wand cast: action is consumed via ConsumeScrollAfterCast/ConsumeWandChargeAfterCast (called from ConsumePendingSpellSlot).
            Debug.Log($"[{(isWandCast ? "WandCast" : "ScrollCast")}] {caster.Stats.CharacterName} casting from {(isWandCast ? "wand" : "scroll")} — action consumed via post-cast handler.");
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, isWandCast ? "wand" : "scroll");
        }
        else if (isDeliveringHeldCharge)
        {
            // Discharging a held touch spell costs no action here, in any round (SPL-156).
            RequireLegacyQuirk(ctx, LegacyCastQuirk.HeldChargeDeliveryIsFree);
            Debug.Log($"[GameManager] {caster.Stats.CharacterName} discharging held touch spell as a free action.");
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, "held-charge-delivery");
        }
        else if (actionCost == CastActionCost.Free)
        {
            // The caster's one quickened spell of the round (PHB p.98; Feather Fall counts toward it, PHB p.229).
            var casterSpellComp = caster.Spellcasting;
            if (casterSpellComp != null)
                casterSpellComp.MarkQuickenedSpellCast();
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, isQuickened ? "quickened" : "free");
        }
        else if (actionCost == CastActionCost.FullRound)
        {
            caster.Actions.UseFullRoundAction();
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, "full-round");
        }
        else if (actionCost == CastActionCost.Swift)
        {
            caster.Actions.UseSwiftAction();
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, "swift");
        }
        else
        {
            if (!caster.CommitStandardAction() && req.HasQuirk(LegacyCastQuirk.NpcOwnLegalityChecks))
            {
                // The NPC path refuses a cast whose standard action cannot be committed (SPL-160); nothing was spent.
                CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, "commit-failed");
                ClearSpellcastResourceSnapshot();
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Refused, CastStage.Cost, "commit-failed"));
                return;
            }
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, "standard");
        }
        // Test instrumentation (inert outside the scenario harness): a cast begins. Delivering a held charge is not a
        // cast (the hold raised it when the spell was cast). A summon, Grease or hold has no target creature here.
        if (!isDeliveringHeldCharge)
            ScenarioHooks.SpellCast?.Invoke(caster, target, spell);

        SpellcastingComponent spellComp = caster.Spellcasting;
        if (spellComp == null)
        {
            RefundAfterFailedSpend(ctx);
            Debug.LogError($"[GameManager] {(IsAreaCast(ctx) ? "PerformAoESpellCast" : "PerformSpellCast")}: No SpellcastingComponent!");
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Cost, CastOutcome.ReasonNoSpellcasting));
            return;
        }
        ctx.SpellComp = spellComp;

        // A cleric converting a specific prepared spell: the cast's own source, which the spell menu filled once from its
        // Convert button (batch 11 step 4), so a cancelled conversion cannot make a later cast spontaneous. A hold of the
        // converted spell spends the sacrificed spell too (PHB p.32, p.141; batch 11 step 14, SPL-154).
        bool isSpontaneous = !isDeliveringHeldCharge && sourceKind == CastSourceKind.SpontaneousConversion;

        // The slot to spend; 0-level spells spend a slot too (SPL-138).
        int slotLevelToConsume = spell.SpellLevel;
        bool hasMetamagicApplied = metamagic != null && metamagic.HasAnyMetamagic;
        if (hasMetamagicApplied)
        {
            slotLevelToConsume = metamagic.GetEffectiveSpellLevel(spell.SpellLevel);
            Debug.Log($"[GameManager] Metamagic: consuming level {slotLevelToConsume} slot " +
                      $"(base {spell.SpellLevel} + {slotLevelToConsume - spell.SpellLevel} metamagic)");
        }
        ctx.HasMetamagicApplied = hasMetamagicApplied;
        ctx.SlotLevelToConsume = slotLevelToConsume;

        // A metamagic conversion gives up a prepared spell of the adjusted level (PHB p.88: an empowered cure critical
        // wounds for a prepared 6th-level spell; batch 11 step 21, SPL-148): the spell the Convert button named is kept as
        // the sacrifice only when it is of that level, else any convertible spell of that level is given up.
        ctx.IsSpontaneousConversion = isSpontaneous;
        ctx.SpontaneousLevel = isSpontaneous ? req.Source.ConversionLevel : -1;
        ctx.SpontaneousSacrificedSpellId = isSpontaneous && (!hasMetamagicApplied || req.Source.ConversionLevel == slotLevelToConsume)
            ? req.Source.SacrificedSpellId
            : null;

        // The result log's quickened and spontaneous prefixes, built once (SPL-063): quickened first, then spontaneous.
        string prefix = "";
        if (isQuickened)
            prefix += $"⚡ {caster.Stats.CharacterName} casts QUICKENED {spell.Name}! (Free Action)\n";
        if (isSpontaneous)
        {
            string sacrificeInfo = !string.IsNullOrEmpty(ctx.SpontaneousSacrificedSpellId)
                ? $"Sacrificed: {ctx.SpontaneousSacrificedSpellId}"
                : "Converted prepared spell";
            prefix += $"⟳ {caster.Stats.CharacterName} spontaneously casts {spell.Name}! ({sacrificeInfo})\n";
        }
        ctx.LogPrefix = prefix;

        CastStageDistraction(ctx);
    }

    /// <summary>
    /// Distraction: the entangled Concentration check, then grappled or pinned Concentration, for every casting from a
    /// slot or a scroll on every path (batch 11 step 13: an area cast and a hold make the entangled check too, SPL-006,
    /// SPL-146), not for a held-charge delivery (no casting, PHB p.141) or a wand use (spell trigger, no casting;
    /// CastComponentRules.CastingDistractionApplies, delegated RAW reading 2026-10-14). Then the Failure stage.
    /// </summary>
    private void CastStageDistraction(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Distraction);
        if (!ctx.IsHeldChargeDelivery && CastComponentRules.CastingDistractionApplies(ctx.Request.Source))
        {
            SpellCastRequest req = ctx.Request;
            CharacterController caster = req.Caster;
            SpellData spell = req.Spell;

            // An entangled caster: Concentration DC 15 + the spell's level for any spell (PHB p.308, p.70; SPL-145).
            if (!RollEntangledCastingConcentration(caster, spell, req.Metamagic, out int entangledDc))
            {
                LoseCastAtDistraction(ctx, "entangled", "Entangled", "entangled", entangledDc);
                return;
            }

            // A grappled or pinned caster: Concentration DC 20 + the spell's level (PHB p.156).
            if (!RollGrappledOrPinnedCastingConcentration(caster, spell, out int grappleDc, out string conditionLabel))
            {
                LoseCastAtDistraction(ctx, "grappled", conditionLabel, conditionLabel, grappleDc);
                return;
            }
        }
        else if (!ctx.IsHeldChargeDelivery)
        {
            // A wand use makes no casting check, but a pinner who uses one on its renewal turn does not pin again, as
            // for a cast (PHB p.156, CMB-120; RollGrappledOrPinnedCastingConcentration does this for a casting).
            string pinLapse = ctx.Request.Caster.LetDuePinLapseIfDue();
            if (!string.IsNullOrEmpty(pinLapse))
                CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", pinLapse));
        }

        CastStageFailure(ctx);
    }

    /// <summary>
    /// A spell lost to a failed casting Concentration check at the Distraction stage (entangled, grappled or pinned): its
    /// slot (or scroll, converted spell) is spent and the loss is logged, the casting-while-concentrating step runs, and
    /// the cast ends Interrupted. A slot that cannot be spent restores the snapshot and ends the cast with
    /// <see cref="CastOutcome.ReasonSlotNotSpent"/>, as <see cref="LoseCastAtFailure"/> does (batch 11 step 14: no
    /// spell was cast, so the action is given back).
    /// </summary>
    private void LoseCastAtDistraction(CastContext ctx, string reason, string errorLabel, string conditionLabel, int dc)
    {
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        if (!SpendCastSlot(ctx))
        {
            RefundAfterFailedSpend(ctx);
            Debug.LogError($"[GameManager] {errorLabel} concentration failure path: could not consume level {ctx.SlotLevelToConsume} slot for {spell.Name}");
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Distraction, CastOutcome.ReasonSlotNotSpent));
            return;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{caster.Stats.CharacterName} fails the DC {dc} concentration check while {conditionLabel}. {spell.Name} is lost and the spell slot is spent."));
        CheckConcentrationOnLostCast(ctx);
        ClearSpellcastResourceSnapshot();
        FinishCast(ctx, LostCastOutcome(CastOutcomeKind.Interrupted, CastStage.Distraction, reason));
    }

    /// <summary>
    /// Failure: one roll each per casting, for every cast but a held-charge delivery, on every path and for every caster
    /// (batch 11 step 13): arcane spell failure (a somatic component, a slot or a scroll; TryRollArcaneSpellFailure,
    /// SPL-096, SPL-116), the blinking caster's 20% (PHB p.206; SpellResolutionService.TryBlinkCasterFailure, SPL-006,
    /// SPL-146) and the deafened caster's verbal failure (PHB p.307; CastComponentRules.TryDeafenedFailure, SPL-150). A
    /// failed spell spends its slot here (<see cref="LoseCastAtFailure"/>); a cast that passes goes on to the Spend stage.
    /// </summary>
    private void CastStageFailure(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Failure);
        if (!ctx.IsHeldChargeDelivery)
        {
            SpellCastRequest req = ctx.Request;
            CharacterController caster = req.Caster;
            SpellData spell = req.Spell;

            if (TryRollArcaneSpellFailure(caster, spell, req.Metamagic, req.Source, out int asfRoll, out int asfChance))
            {
                LoseCastAtFailure(ctx, "arcane-spell-failure", IsAreaCast(ctx) ? "AoE ASF failure path" : "ASF failure path",
                    () => LogArcaneSpellFailure(caster, spell, asfRoll, asfChance));
                return;
            }

            // PHB p.206: a blinking caster's spell has a 20% chance to activate just as the caster goes ethereal, checked
            // separately from and in addition to arcane spell failure.
            if (SpellResolutionService.TryBlinkCasterFailure(caster, spell.Name, out int blinkSpellRoll))
            {
                CastStageTrace.Mark(caster, spell, CastStageTrace.Failure, "blink-failed");
                LoseCastAtFailure(ctx, "blink-caster", "Blink spell failure path",
                    () => CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡", $"{caster.Stats.CharacterName}'s {spell.Name} fizzles! (Blink spell failure: rolled {blinkSpellRoll} ≤ 20%)")));
                return;
            }
            if (blinkSpellRoll > 0)
            {
                CastStageTrace.Mark(caster, spell, CastStageTrace.Failure, "blink-passed");
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡", $"Blink spell check: {caster.Stats.CharacterName} rolled {blinkSpellRoll} > 20% (spell proceeds)."));
            }

            // PHB p.307: a deafened caster has a 20% chance of spell failure with a verbal component, once per casting.
            if (CastComponentRules.TryDeafenedFailure(caster, spell, req.Metamagic, req.Source, out int deafRoll, out int deafChance))
            {
                CastStageTrace.Mark(caster, spell, CastStageTrace.Failure, "deafened-failed");
                LoseCastAtFailure(ctx, "deafened", "Deafened spell failure path",
                    () => CombatUI?.ShowCombatLog(CombatLogHelper.Failure("🔇", $"{caster.Stats.CharacterName}'s {spell.Name} fails: deafened, the verbal component goes wrong (rolled {deafRoll} ≤ {deafChance}%, PHB p.307). The spell is lost.")));
                return;
            }
            if (deafRoll > 0)
            {
                CastStageTrace.Mark(caster, spell, CastStageTrace.Failure, "deafened-passed");
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🔇", $"Deafened spell check: {caster.Stats.CharacterName} rolled {deafRoll} > {deafChance}% (spell proceeds)."));
            }
        }

        CastStageSpend(ctx);
    }

    /// <summary>
    /// A spell lost at the Failure stage: its slot (or scroll, wand charge, converted spell) is spent, the
    /// casting-while-concentrating step runs, <paramref name="log"/> shows why, and the cast ends Failed. A slot that
    /// cannot be spent ends the cast with <see cref="CastOutcome.ReasonSlotNotSpent"/> instead.
    /// </summary>
    private void LoseCastAtFailure(CastContext ctx, string reason, string errorLabel, Action log)
    {
        if (!SpendCastSlot(ctx))
        {
            RefundAfterFailedSpend(ctx);
            Debug.LogError($"[GameManager] {errorLabel}: failed to consume level {ctx.SlotLevelToConsume} spell slot!");
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Failure, CastOutcome.ReasonSlotNotSpent));
            return;
        }

        CheckConcentrationOnLostCast(ctx);
        log?.Invoke();
        ClearSpellcastResourceSnapshot();
        FinishCast(ctx, LostCastOutcome(CastOutcomeKind.Failed, CastStage.Failure, reason));
    }

    /// <summary>
    /// Spend: the slot (or the scroll, the wand charge, the converted spell), not for a held-charge delivery, after the
    /// failure checks for every caster (batch 11 step 13 removed the NPC path's spend before them, SPL-160). Then the
    /// provocation.
    /// </summary>
    private void CastStageSpend(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Spend);
        if (!ctx.IsHeldChargeDelivery)
        {
            CharacterController caster = ctx.Request.Caster;
            SpellData spell = ctx.Request.Spell;
            if (!SpendCastSlot(ctx))
            {
                RefundAfterFailedSpend(ctx);
                Debug.LogError($"[GameManager] {(IsAreaCast(ctx) ? "AoE: " : "")}Failed to consume level {ctx.SlotLevelToConsume} spell slot!");
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Spend, CastOutcome.ReasonSlotNotSpent));
                return;
            }

            if (ctx.IsSpontaneousConversion && !string.IsNullOrEmpty(ctx.SpontaneousSacrificedSpellId))
                Debug.Log($"[GameManager] Spontaneous cast: {caster.Stats.CharacterName} sacrificed '{ctx.SpontaneousSacrificedSpellId}' → {spell.Name}");
            else if (ctx.IsSpontaneousConversion)
                Debug.Log($"[GameManager] Spontaneous cast (level-based): {caster.Stats.CharacterName} converted a level {ctx.SpontaneousLevel} slot → {spell.Name}");
        }

        CastStageAfterSpend(ctx);
    }

    /// <summary>
    /// After the spend and the failure checks: the provocation. The concentration step follows once the provocation lets
    /// the cast proceed (batch 11 step 12, SPL-152), so a cast cancelled at the AoO prompt has rolled nothing and ended
    /// no maintained spell.
    /// </summary>
    private void CastStageAfterSpend(CastContext ctx)
    {
        CastStageProvocation(ctx);
    }

    /// <summary>
    /// ConcentrationOnCast: a caster concentrating on another spell stops concentrating as it casts, a free action with
    /// no roll, and a "+N rounds" spell runs its tail (HandleConcentrationOnCasting; decision D1, batch 11 step 16, PHB
    /// p.176), once per run, for every caster (batch 11 step 15, SPL-114). A threatened cast that proceeds has done it
    /// already, before its attacks of opportunity (<see cref="EndConcentrationForCasting"/>); a cast cancelled at the AoO
    /// prompt ends nothing (SPL-152).
    /// </summary>
    private void CastStageConcentrationOnCast(CastContext ctx, Action<CastContext> next)
    {
        EnterCastStage(ctx, CastStage.ConcentrationOnCast);
        EndConcentrationForCasting(ctx);
        next(ctx);
    }

    /// <summary>
    /// Runs the casting-while-concentrating step of <paramref name="ctx"/> once (decision D1): the deciders call it when
    /// a threatened caster has chosen to cast, before the defensive roll or the attacks of opportunity, since the caster
    /// stopped concentrating to begin casting; the ConcentrationOnCast stage and a lost cast call it otherwise.
    /// </summary>
    internal void EndConcentrationForCasting(CastContext ctx)
    {
        if (ctx == null || ctx.ConcentrationHandled)
            return;
        ctx.ConcentrationHandled = true;
        HandleConcentrationOnCasting(ctx.Request.Caster, ctx.Request.Spell, ctx.Request.Source);
    }

    /// <summary>
    /// Provocation: a threatened cast provokes attacks of opportunity (PHB p.140); the decider chooses to cast
    /// defensively, cast normally or cancel and resolves the rolls. No provocation for a held-charge delivery, nor for a
    /// quickened cast (PHB p.98: casting a quickened spell does not provoke; SPL-143) or a spell whose casting time is a
    /// free action (Feather Fall; PHB p.174: such a casting does not provoke), unless spontaneous metamagic made it a
    /// full-round action (CastActionRules.CountsAsQuickenedSpell).
    /// </summary>
    private void CastStageProvocation(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Provocation);
        bool provokes = !ctx.IsHeldChargeDelivery && !ctx.CountsAsQuickenedSpell;
        if (!provokes)
        {
            _spellcastProvocationCancelled = false;
            CastStageTrace.Mark(ctx.Request.Caster, ctx.Request.Spell, CastStageTrace.Provocation, "no-provocation");
            OnCastProvocationAnswered(ctx, CastProvocationAnswer.Proceeds);
            return;
        }

        // The run waits for the decider's answer (the PC's AoO prompt): a re-entry now would wipe the run's state.
        ctx.AwaitingDecider = true;
        ctx.Request.Decider.ChooseProvocation(this, ctx, answer => OnCastProvocationAnswered(ctx, answer));
    }

    private void OnCastProvocationAnswered(CastContext ctx, CastProvocationAnswer answer)
    {
        ctx.AwaitingDecider = false;
        CharacterController caster = ctx.Request.Caster;
        if (answer == CastProvocationAnswer.Cancelled)
        {
            // Backing out at the prompt gives back the action and the spell (the snapshot).
            RestoreSpellcastResourceSnapshot(caster);
            _spellcastProvocationCancelled = false;
            if (caster != null && caster.Stats != null)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{caster.Stats.CharacterName} cancels spell cast."));
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Cancelled, CastStage.Provocation, "cancelled"));
            return;
        }

        ClearSpellcastResourceSnapshot();
        if (answer == CastProvocationAnswer.Lost)
        {
            // A spell lost to the attacks of opportunity runs the concentration step as every other lost cast does.
            CheckConcentrationOnLostCast(ctx);
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Interrupted, CastStage.Provocation, "lost"));
            return;
        }

        CastStageConcentrationOnCast(ctx, CastStageBeforeResolution);
    }

    /// <summary>
    /// The pre-resolution checks after a proceeding provocation, in one order for every caster and path (decision D4,
    /// batch 11 step 15, SPL-118): the readied counterspell, then the hostile-cast invisibility break, then the Ring of
    /// Counterspells. A readied counterspell interrupts the casting before a target is chosen or an area placed (PHB
    /// p.160, p.170-171, p.175), so a countered spell leaves its caster invisible; the ring acts on a spell already cast
    /// on its wearer (DMG p.230), so a spell the ring absorbs has ended the caster's invisibility (PHB p.245); a counter
    /// that fails lets the spell resolve as an attack (delegated RAW reading 2026-10-14).
    /// </summary>
    private void CastStageBeforeResolution(CastContext ctx)
    {
        CastStageCounterspell(ctx);
    }

    /// <summary>
    /// Counterspell: a readied counterspell (PHB p.170) stops the spell, on every path: a single target, an area, a summon,
    /// Grease and a hold (batch 11 step 15, SPL-006, SPL-146; the held charge is the spell already cast, PHB p.141, so it
    /// is countered at the hold). Not for a held-charge delivery, which is no casting (PHB p.141). A countered spell ends
    /// the cast here, before the invisibility break (decision D4).
    /// </summary>
    private void CastStageCounterspell(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Counterspell);
        if (!ctx.IsHeldChargeDelivery)
        {
            CharacterController caster = ctx.Request.Caster;
            SpellData spell = ctx.Request.Spell;
            CounterspellResult counterspellResult = TryResolveCounterspell(caster, spell);
            CastStageTrace.Mark(caster, spell, CastStageTrace.Counterspell,
                counterspellResult == null ? "none" : counterspellResult.Success ? "countered" : "not-countered");
            if (counterspellResult != null && counterspellResult.Success)
            {
                Debug.Log($"[Counterspell] {spell.Name} was countered! No spell effect.");
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Counterspell, "countered"));
                return;
            }
            // A failed counterspell (a Dispel Magic miss) lets the spell go on.
        }

        CastStageInvisibilityBreak(ctx);
    }

    /// <summary>
    /// RingCounterspell, after the invisibility break (decision D4): a Ring of Counterspells on the target holding this
    /// spell counters it (DMG p.230: the stored spell "cast upon the wearer"). Only a single-target cast (a held charge's
    /// delivery included, whose target is chosen when it is delivered) has a creature the spell is cast upon: an area spell
    /// is cast at an area, not on the creatures in it (PHB p.175), so the ring stops neither the whole spell nor its effect
    /// on a wearer in the area; a summon, a Grease (an area or an object) and a hold target no creature, nor does a burst
    /// or emanation centred on the caster (Prayer, Invisibility Purge, Zone of Truth: <see cref="RingMeetsSingleCast"/>)
    /// (delegated RAW reading 2026-10-14; batch 11 step 15 removed AreaSkipsRing, SPL-006). The ring counters "immediately,
    /// as a counterspell action", so a touch spell meets it before its touch attack; a held charge countered at its
    /// delivery is gone, as a countered spell is (PHB p.170-171), so the charge is cleared. Then the request's resolve variant.
    /// </summary>
    private void CastStageRingCounterspell(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.RingCounterspell);
        if (ctx.Request.ResolveKind == CastResolveKind.Single && RingMeetsSingleCast(ctx.Request.Spell))
        {
            CharacterController caster = ctx.Request.Caster;
            CharacterController target = ctx.Request.Aim.Target;
            SpellData spell = ctx.Request.Spell;
            if (target != null && spell != null && CounterspellManager.TryRingCounterspell(target, spell))
            {
                CastStageTrace.Mark(caster, spell, CastStageTrace.Ring, "countered");
                Debug.Log($"[RingCounterspell] {spell.Name} countered by Ring of Counterspells on {target.Stats?.CharacterName}!");
                if (ctx.IsHeldChargeDelivery)
                    ctx.SpellComp?.ClearHeldTouchCharge("countered by Ring of Counterspells");
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.RingCounterspell, "ring"));
                return;
            }
        }

        CastStageResolveVariant(ctx);
    }

    /// <summary>
    /// False for a spell resolved on the single-target path whose effect is a burst or emanation centred on its caster
    /// (Target "You" with an area: Prayer, Invisibility Purge, Zone of Truth): it is cast at an area, not upon a creature,
    /// so a Ring of Counterspells does not meet it (DMG p.230, PHB p.175; delegated RAW reading 2026-10-14). True for every
    /// other single-target cast, including an emanation centred on a creature the spell is cast upon (Silence on a
    /// creature, which gets a save, PHB p.279; Invisibility Sphere and Magic Circle on the creature touched).
    /// </summary>
    internal static bool RingMeetsSingleCast(SpellData spell)
    {
        if (spell == null)
            return false;
        bool centredOnCasterArea = spell.TargetType == SpellTargetType.Self
                                   && (spell.AoEShapeType != AoEShape.None || spell.AreaRadius > 0 || CutsGlobeOutOfOwnBurst(spell));
        return !centredOnCasterArea;
    }

    /// <summary>
    /// After the pre-resolution checks (the Ring of Counterspells last), the request's resolve variant: the single-target stages from Mirror Image on, the
    /// area Resolve stage (<see cref="CastResolveKind.Area"/>), or the Summon, Grease and HoldCharge Resolve stages.
    /// </summary>
    private void CastStageResolveVariant(CastContext ctx)
    {
        switch (ctx.Request.ResolveKind)
        {
            case CastResolveKind.Area:
                CastStageResolveArea(ctx);
                break;
            case CastResolveKind.Summon:
                CastStageResolveSummon(ctx);
                break;
            case CastResolveKind.Grease:
                CastStageResolveGrease(ctx);
                break;
            case CastResolveKind.HoldCharge:
                CastStageResolveHoldCharge(ctx);
                break;
            default:
                CastStageMirrorImage(ctx);
                break;
        }
    }

    /// <summary>
    /// InvisibilityBreak: a hostile cast ends the caster's invisibility (PHB p.245). Here too, as before, the cast settles
    /// whether a willing target skips the touch attack and whether the target forgoes its save (both read only the
    /// creatures). It runs after the readied counterspell and before the Ring of Counterspells (decision D4); then the
    /// RingCounterspell stage.
    /// </summary>
    private void CastStageInvisibilityBreak(CastContext ctx)
    {
        // An area spell's creatures lose the ones a globe protects first: the area does not include the globe (PHB p.236),
        // so a foe inside one does not make the spell an attack (PHB p.245).
        if (IsAreaCast(ctx))
            CastStageAreaGlobe(ctx);

        EnterCastStage(ctx, CastStage.InvisibilityBreak);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;

        if (ctx.Request.ResolveKind != CastResolveKind.Single)
        {
            switch (ctx.Request.ResolveKind)
            {
                case CastResolveKind.Area:
                    // An area spell is hostile when it reaches a foe in the area; the touch and forgone-save tests below
                    // read a single target, and the area handlers make their own.
                    BreakInvisibilityOnHostileSpellCast(caster, spell, null, ctx.Request.Aim.AreaTargets);
                    break;
                case CastResolveKind.Grease:
                    // Grease is an attack when its square takes in a foe, or when it greases an object a foe holds or
                    // wears: PHB p.245 spares only actions directed at unattended objects, and Grease gives the wielder
                    // of the object a save (PHB p.237) (delegated RAW reading 2026-10-14; batch 11 step 15, SPL-006).
                    GreaseCastMode greaseMode = ctx.Request.Choices.GreaseMode;
                    if (greaseMode == GreaseCastMode.Object || greaseMode == GreaseCastMode.Armor)
                        BreakInvisibilityOnHostileSpellCast(caster, spell, ctx.Request.Aim.ObjectHolder, null);
                    else
                        BreakInvisibilityOnHostileSpellCast(caster, spell, null, ctx.Request.Aim.AreaTargets);
                    break;
                default:
                    // A summon or a hold targets no creature. A summoned creature, a swarm laid on a foe's square included,
                    // harms only indirectly, which is no attack: PHB p.245 names "summon monsters and have them attack"
                    // (delegated RAW reading 2026-10-14). A held charge attacks when it is delivered.
                    break;
            }

            CastStageRingCounterspell(ctx);
            return;
        }

        BreakInvisibilityOnHostileSpellCast(caster, spell, target, null);
        // A willing friendly target of a melee touch spell needs no touch attack.
        ctx.SkipFriendlyTouchAttackRoll = spell.IsMeleeTouchSpell() && IsFriendlyTarget(caster, target);
        // The target's own decider says whether it gives up its save (PHB p.177; batch 11 step 18, SPL-149): only against
        // a harmless or beneficial spell of its own side.
        ctx.ForgoesSingleTargetSave = ctx.TargetForgoesSave(target);

        CastStageRingCounterspell(ctx);
    }

    /// <summary>MirrorImage: a targeted spell against a creature with figments may strike one instead (PHB p.254).</summary>
    private void CastStageMirrorImage(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.MirrorImage);
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        if (TryHandleMirrorImageSpellTargetAttack(caster, ctx.Request.Aim.Target, spell, out string mirrorSpellLog))
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.MirrorImage, "struck-image");
            ctx.ResultLog = mirrorSpellLog;
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Resolved, CastStage.MirrorImage, "struck-image"));
            return;
        }

        CastStageTargetBlink(ctx);
    }

    /// <summary>
    /// TargetBlink: an individually targeted spell has a 50% chance to fail against a blinking creature (PHB p.206); not
    /// for area or self spells (SpellResolutionService.TryBlinkTargetFailure, one roll, label and log for every caster,
    /// batch 11 step 13).
    /// </summary>
    private void CastStageTargetBlink(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.TargetBlink);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;
        bool missed = SpellResolutionService.TryBlinkTargetFailure(caster, target, spell, out int blinkTargetRoll);
        if (blinkTargetRoll > 0)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.TargetBlink, missed ? "missed" : "connects");
            string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
            if (missed)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{spell.Name} fails to reach {targetName}! Target is on the Ethereal Plane. (Blink: rolled {blinkTargetRoll} ≤ 50%)"));
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.TargetBlink, "target-blinked"));
                return;
            }

            CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"Blink target check: {targetName} rolled {blinkTargetRoll} > 50% (spell connects)."));
        }

        CastStageGlobe(ctx);
    }

    /// <summary>
    /// Globe (PHB p.236): a lesser globe of invulnerability (3rd level or lower) or a globe of invulnerability (4th or
    /// lower) around the target keeps the spell from affecting it, the caster's own spells included ("such spells fail
    /// to affect any target located within the globe"). The test runs before SpellCaster.Cast, so a covered target is
    /// not attacked and takes no damage or healing, and no handler or tracked effect runs: the cast ends Failed here,
    /// its slot and action spent (batch 11 step 11, SPL-141). Three casts go on to Resolve instead:
    /// a targeted Dispel Magic, which PHB p.236 lets bring the globe down (ApplySpellBuff then dispels only the globe,
    /// DispelMagicService.PerformGlobeDispel); a melee touch spell, whose touch is still made (a touch that lands
    /// discharges the spell with no effect, a missed one keeps a held charge, PHB p.141-142; SpellCaster.Cast); and a
    /// spell whose handler resolves a burst or emanation around the target creature by creature
    /// (<see cref="CutsGlobeOutOfOwnBurst"/>: the globe is cut out of the area, the rest of it is affected). The last two
    /// are delegated RAW readings 2026-10-14. The globe compares the spell's base level (Heighten, PHB p.95, is an open
    /// part of SPL-141).
    /// </summary>
    private void CastStageGlobe(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Globe);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;
        if (ctx.IsBlockedByGlobe(target, out LesserGlobeOfInvulnerabilityAreaEffect blockingGlobe))
        {
            if (string.Equals(spell.SpellId, SpellNames.DISPEL_MAGIC, StringComparison.Ordinal))
            {
                CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "dispel-at-globe");
                CastStageResolve(ctx);
                return;
            }

            if (CutsGlobeOutOfOwnBurst(spell))
            {
                CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "burst-excludes-globe");
                CastStageResolve(ctx);
                return;
            }

            if (spell.IsMeleeTouchSpell())
            {
                ctx.GlobeAgainstTouch = blockingGlobe;
                CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "touch-first");
                CastStageResolve(ctx);
                return;
            }

            CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "blocked");
            string casterName = caster.Stats != null ? caster.Stats.CharacterName : caster.name;
            ctx.ResultLog = ctx.LogPrefix + CombatLogHelper.Defensive("🛡",
                $"{casterName} casts {spell.Name} (level {spell.SpellLevel}) at {target.Stats.CharacterName}, but {LesserGlobeOfInvulnerabilityAreaEffect.DescribeBlock(blockingGlobe)} around {target.Stats.CharacterName} blocks it: the spell has no effect.");
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Globe, "globe-blocked"));
            return;
        }

        CastStageResolve(ctx);
    }

    /// <summary>
    /// The spells resolved on the single-target path whose handler gives a burst or emanation around the aimed creature
    /// to each creature in it and leaves out every creature a globe of invulnerability protects (PHB p.236: the area of
    /// such a spell does not include the globe): Prayer (PHB p.264) and Invisibility Purge (PHB p.245), both centred on
    /// the caster. The Globe stage does not fail them when the caster stands inside a globe. Other single-target-routed
    /// spells with an area around the target (Invisibility Sphere, Magic Circle against Evil, Zone of Truth, Silence,
    /// Resilient Sphere's extra creatures) are still decided whole by the target (SPL-141's open part).
    /// </summary>
    internal static bool CutsGlobeOutOfOwnBurst(SpellData spell)
    {
        return spell != null
               && (string.Equals(spell.SpellId, SpellNames.PRAYER, StringComparison.Ordinal)
                   || string.Equals(spell.SpellId, SpellNames.INVISIBILITY_PURGE, StringComparison.Ordinal));
    }

    /// <summary>
    /// True when a globe of invulnerability keeps <paramref name="spell"/>'s burst from <paramref name="creature"/>, which
    /// the handler of a <see cref="CutsGlobeOutOfOwnBurst"/> spell then skips, with a log line (PHB p.236; SPL-141).
    /// </summary>
    private bool SkipCreatureInsideGlobe(CharacterController caster, CharacterController creature, SpellData spell)
    {
        if (!LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(spell, creature, out LesserGlobeOfInvulnerabilityAreaEffect globe))
            return false;
        string name = creature.Stats != null ? creature.Stats.CharacterName : creature.name;
        CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "blocked " + name);
        CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡",
            $"{spell.Name} (level {spell.SpellLevel}) does not reach {name}: {LesserGlobeOfInvulnerabilityAreaEffect.DescribeBlock(globe)} keeps it out."));
        return true;
    }

    /// <summary>
    /// Resolve: SpellCaster.Cast (attack roll, spell resistance, save, generic damage or healing), then the save's and
    /// the mind-affecting immunity's log lines; the Handlers stage follows. A melee touch at a creature inside a globe
    /// (<see cref="CastContext.GlobeAgainstTouch"/>) lands with no effect when it hits (SpellCaster.Cast).
    /// </summary>
    private void CastStageResolve(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Resolve);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;

        SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, ctx.Request.Metamagic, ctx.SkipFriendlyTouchAttackRoll, ctx.ForgoesSingleTargetSave, caster, target, ctx.GlobeAgainstTouch);
        ctx.Result = result;
        CastStageTrace.Mark(caster, spell, CastStageTrace.SpellCasterCast, result.Success ? "success" : "failed");
        if (result.BlockedByGlobe)
            CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "blocked-on-touch");

        // The landed cast's effect step (ApplySpellBuff): every Buff, Debuff, Control, Illusion and Wall spell, and the
        // other spells ApplySpellBuff has a branch for. The same for every caster (SPL-037).
        ctx.AppliesTrackedEffect = SpellEffectRouting.ReachesApplySpellBuff(spell);

        // The same for every caster (SPL-007): Debuff and Control saves negate; Cause Fear and Scare are
        // partial; nonintelligent undead get no save vs Command Undead.
        bool effectNegatedBySave = SpellUtilities.IsEffectNegatedBySave(spell, result, target != null && !target.IsIntelligentUndead());
        ctx.EffectNegatedBySave = effectNegatedBySave;
        if (effectNegatedBySave)
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{target.Stats.CharacterName} resists {spell.Name} with a successful {result.SaveType} save."));

        if (result.MindAffectingImmunityBlocked)
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧠", $"{target.Stats.CharacterName} is immune to mind-affecting effects. {spell.Name} has no effect."));

        CastStageHandlers(ctx);
    }

    /// <summary>
    /// Handlers: the SpellId registry (GameManager.SpellHandlerRegistry.cs) runs the handler registered for the spell when
    /// its gate passes. A claimed cast skips the Buff stage; Shield Other goes on to it. A failed
    /// cast (spell resistance, a deafened caster's verbal failure, a missed touch) lands no handler's effect: the gate, or
    /// for the gate-None handlers (Cause Fear, Ghoul Touch, Scare, Animate Rope) the handler's own test (SPL-142).
    /// </summary>
    private void CastStageHandlers(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Handlers);
        SpellData spell = ctx.Request.Spell;
        ctx.ClaimedByHandler = RunSingleTargetHandlers(ctx.Request.Caster, ctx.Request.Aim.Target, spell, ctx.Result, ctx.EffectNegatedBySave, out _);
        CastStageBuff(ctx);
    }

    /// <summary>
    /// Buff: an unclaimed, successful cast that a save did not negate and that reaches ApplySpellBuff gets its tracked
    /// effect; a Concentration-duration effect starts the caster's concentration.
    /// </summary>
    private void CastStageBuff(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Buff);
        if (!ctx.ClaimedByHandler && ctx.Result.Success && ctx.AppliesTrackedEffect && !ctx.EffectNegatedBySave)
        {
            CharacterController caster = ctx.Request.Caster;
            SpellData spell = ctx.Request.Spell;
            var appliedEffect = ApplySpellBuff(caster, ctx.Request.Aim.Target, spell, ctx.SpellComp, ctx.Result);
            CastStageTrace.Mark(caster, spell, CastStageTrace.Buff, appliedEffect != null ? "applied" : "no-effect");

            // Every caster's Concentration-duration effect starts its concentration (batch 11 step 15, SPL-114), so damage
            // can end it (PHB p.176).
            if (appliedEffect != null && spell.DurationType == DurationType.Concentration)
                BeginConcentrationTracking(caster, appliedEffect, spell);
        }

        CastStagePostCast(ctx);
    }

    /// <summary>
    /// PostCast: the target's concentration against the spell's damage, the held charge (kept when the delivering touch
    /// misses, PHB p.141), the target's death, and the cast's result log with its prefixes (built, and shown at once,
    /// before the death with <see cref="LegacyCastQuirk.NpcResultLogBeforeTargetDeath"/>).
    /// </summary>
    private void CastStagePostCast(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.PostCast);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;
        SpellResult result = ctx.Result;

        if (result.DamageDealt > 0 && target != null)
            CheckConcentrationOnDamage(target, result.DamageDealt);

        // Delivering a held charge clears it only if the touch actually lands; a missed touch keeps the charge.
        if (ctx.IsHeldChargeDelivery)
        {
            bool touchDeliverySucceeded = !result.RequiredAttackRoll || result.AttackHit;
            if (touchDeliverySucceeded)
            {
                ctx.SpellComp.ClearHeldTouchCharge("touch delivered");
            }
            else
            {
                ctx.RetainedHeldChargeOnMiss = true;
                CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, "held-charge-kept");
            }
        }

        // The NPC path shows its result log before the target's death (NpcResultLogBeforeTargetDeath, SPL-160).
        bool logBeforeDeath = ctx.Request.HasQuirk(LegacyCastQuirk.NpcResultLogBeforeTargetDeath);
        if (logBeforeDeath)
        {
            BuildCastResultLog(ctx);
            FlushCastLog(ctx);
        }

        if (result.TargetKilled && target != null)
        {
            target.OnDeath();
            HandleSummonDeathCleanup(target);
        }

        if (!logBeforeDeath)
            BuildCastResultLog(ctx);

        CastStageCombatEnd(ctx);
    }

    /// <summary>The cast's result log: its prefixes, SpellCaster's log and, for a missed held-charge touch, the kept charge.</summary>
    private static void BuildCastResultLog(CastContext ctx)
    {
        string log = ctx.LogPrefix + ctx.Result.GetFormattedLog();
        if (ctx.RetainedHeldChargeOnMiss)
            log += $"\n✋ Touch attack missed — {ctx.Request.Caster.Stats.CharacterName} retains {ctx.Request.Spell.Name} charge.";
        ctx.ResultLog = log;
    }

    /// <summary>
    /// CombatEnd: the result log goes out first, then the decider's follow-up that precedes the check (the UI's stat
    /// panels, which also refresh the Flanked conditions, and highlights, in the old order), then the shared combat-end
    /// check (EvaluateCombatEnd), also when a handler dealt the damage through DealDamage without setting
    /// result.TargetKilled (SPL-004). With <see cref="LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath"/> (CORE-039)
    /// only when the target was killed or is out of the fight; with <see cref="LegacyCastQuirk.NpcCombatEndCheckedByCaller"/>
    /// (CORE-039) not here: the NPC wrapper checks after the cast returns.
    /// </summary>
    private void CastStageCombatEnd(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.CombatEnd);
        FlushCastLog(ctx);
        ctx.Request.Decider?.BeforeCombatEndCheck(this, ctx);

        CharacterController target = ctx.Request.Aim.Target;
        SpellResult result = ctx.Result;
        // The NPC wrapper TryNPCPerformSpellCastForAI checks after the cast returns (NpcCombatEndCheckedByCaller, CORE-039).
        bool check = !ctx.Request.HasQuirk(LegacyCastQuirk.NpcCombatEndCheckedByCaller)
                     && (!ctx.Request.HasQuirk(LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath)
                         || result.TargetKilled || (target != null && CombatEndRules.IsOutOfFight(target)));
        if (check && EvaluateCombatEnd("ResolveSingleTargetSpell"))
        {
            CastStageTrace.Mark(ctx.Request.Caster, ctx.Request.Spell, CastStageTrace.CombatEnd, "ResolveSingleTargetSpell");
            ctx.CombatEnded = true;
        }

        FinishCast(ctx, new CastOutcome(CastOutcomeKind.Resolved, CastStage.CombatEnd, ctx.CombatEnded ? "combat-over" : "resolved", result)
        {
            CombatEnded = ctx.CombatEnded
        });
    }

    // ── The area resolve variant (batch 11 step 9) ──────────────────────────────────────────────────────────────

    private static bool IsAreaCast(CastContext ctx) => ctx.Request.ResolveKind == CastResolveKind.Area;

    /// <summary>
    /// The area's line of effect (PHB p.176: an area affects what its origin has line of effect to): the squares and
    /// creatures the area's point of origin cannot reach are dropped from the request's aim, in place (the lists the UI
    /// passed, as before). The origin is the grid intersection the builder recorded (<see cref="CastAim.AreaOrigin"/>: a
    /// burst's or cube's point, a cone's or line's start, the self-centred area's corner, a Grease square's centre; batch 11
    /// step 24, decision D5), so the cast drops exactly the squares the preview dropped. An area with no recorded origin
    /// keeps a square-based origin: a burst or cube the intersection nearest the centre of its squares, a one-square effect
    /// its square, anything else (the walls) the caster's square.
    /// </summary>
    private static void FilterAreaAimByLineOfEffect(SpellCastRequest req)
    {
        SpellData spell = req.Spell;
        CharacterController caster = req.Caster;
        HashSet<Vector2Int> aoeCells = req.Aim.Cells;
        if (spell == null || caster == null || aoeCells == null || aoeCells.Count == 0)
            return;

        GridIntersection? origin = req.Aim.AreaOrigin;
        if (!origin.HasValue && AoESystem.IsPlacedShape(spell.AoEShapeType) && !AoESystem.IsSingleSquarePlacement(spell))
        {
            // No recorded origin (a caller that predates step 24): the intersection nearest the centre of the squares.
            float sumX = 0f, sumY = 0f;
            foreach (Vector2Int c in aoeCells)
            {
                sumX += c.x;
                sumY += c.y;
            }
            origin = GridIntersection.NearestToGridPoint(new Vector2(sumX / aoeCells.Count, sumY / aoeCells.Count));
            req.Aim.AreaOrigin = origin;
        }

        if (origin.HasValue)
        {
            AoESystem.FilterCellsByLineOfEffect(aoeCells, origin.Value);
            AoESystem.FilterTargetsByLineOfEffect(req.Aim.AreaTargets, origin.Value);
            return;
        }

        // A one-square effect keeps its own square (as before step 24); the walls the caster's square.
        Vector2Int loeOrigin = caster.GridPosition;
        if (AoESystem.IsSingleSquarePlacement(spell))
        {
            foreach (Vector2Int c in aoeCells)
            {
                loeOrigin = c;
                break;
            }
        }
        AoESystem.FilterCellsByLineOfEffect(aoeCells, loeOrigin);
        AoESystem.FilterTargetsByLineOfEffect(req.Aim.AreaTargets, loeOrigin);
    }

    /// <summary>
    /// Globe (area; PHB p.236): the area of a spell of a globe's levels does not include the globe, and the spell fails to
    /// affect any creature inside it. Before the invisibility break and any area handler or the generic loop, the aim's
    /// creatures drop every one a globe protects from this spell (<see cref="CastContext.IsBlockedByGlobe"/>), each with
    /// a log line, so every handler that reads <c>Aim.AreaTargets</c> leaves it out and a foe inside a globe does not end
    /// the caster's invisibility (batch 11 step 11, SPL-141). The aim's squares are kept: a persistent area that a
    /// creature inside a globe stands in, or enters later, is not tested (SPL-141's open part).
    /// </summary>
    private void CastStageAreaGlobe(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Globe);
        List<CharacterController> targets = ctx.Request.Aim.AreaTargets;
        if (targets == null || targets.Count == 0)
            return;

        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        for (int i = 0; i < targets.Count; i++)
        {
            CharacterController t = targets[i];
            if (!ctx.IsBlockedByGlobe(t, out LesserGlobeOfInvulnerabilityAreaEffect blockingGlobe))
                continue;

            targets.RemoveAt(i);
            i--;
            string name = t.Stats != null ? t.Stats.CharacterName : t.name;
            CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "blocked " + name);
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡",
                $"{spell.Name} (level {spell.SpellLevel}) does not reach {name}: {LesserGlobeOfInvulnerabilityAreaEffect.DescribeBlock(blockingGlobe)} keeps it out."));
        }
    }

    /// <summary>
    /// Resolve (area): the area handler registry (GameManager.SpellHandlerRegistry.cs) runs the handler registered for
    /// the spell's id, in the old block order, then the generic per-target loop, which takes what no handler claimed. A
    /// claimed cast goes to <see cref="FinishAreaCast"/>; an Async handler (the walls) returns at once and resumes the
    /// cast from its callback (<see cref="ResumeAreaCast"/>).
    /// </summary>
    private void CastStageResolveArea(CastContext ctx)
    {
        // The area Globe stage ran before the invisibility break (CastStageInvisibilityBreak).
        EnterCastStage(ctx, CastStage.Resolve);
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;

        foreach (AreaHandlerEntry entry in AreaHandlersFor(spell.SpellId))
        {
            if (entry.Mark == AreaHandlerMark.BeforeResolve)
                CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, entry.Name);

            ctx.AreaHandler = entry;
            AreaHandlerResult result = entry.Resolve(ctx);
            if (result.IsPending)
                return;
            if (!result.IsClaimed)
                continue;

            if (entry.Mark == AreaHandlerMark.AfterClaim)
                CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, entry.Name);
            FinishAreaCast(ctx, entry, result.Log);
            return;
        }
    }

    /// <summary>The callback of an Async area handler (Wall of Ice, Wall of Force): the cast goes on with its log.</summary>
    private void ResumeAreaCast(CastContext ctx, string log)
    {
        if (ctx == null || ctx.Outcome != null || ctx.AreaHandler == null)
            return;
        FinishAreaCast(ctx, ctx.AreaHandler, log);
    }

    /// <summary>
    /// The single exit of a resolved area cast (SPL-063): the handler's log with the cast's quickened and spontaneous
    /// prefixes, built once (<see cref="CastContext.LogPrefix"/>; without them for the handlers that never had them,
    /// <see cref="LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers"/>, SPL-161), goes out, then the decider's follow-up
    /// that precedes the check (the UI's last combat log, stat panels with their Flanked refresh, and highlights, in the
    /// old order), Rainbow Pattern's concentration call, and the combat-end check under the handler's own source context
    /// (only for the handlers that had one, <see cref="LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers"/>, CORE-039).
    /// FinishCast then ends the cast, and the UI decider paces the turn (AfterAttackDelay) unless the combat ended.
    /// </summary>
    private void FinishAreaCast(CastContext ctx, AreaHandlerEntry entry, string handlerLog)
    {
        if (ctx.Outcome != null)
            return;

        EnterCastStage(ctx, CastStage.CombatEnd);
        SpellCastRequest req = ctx.Request;
        CharacterController caster = req.Caster;
        SpellData spell = req.Spell;

        bool prefixed = !(entry.OmitsLogPrefix && req.HasQuirk(LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers));
        ctx.ResultLog = handlerLog == null ? null : (prefixed ? ctx.LogPrefix : "") + handlerLog;
        ctx.ConsoleLogLabel = entry.ConsoleLogLabel;
        FlushCastLog(ctx);
        req.Decider?.BeforeCombatEndCheck(this, ctx);

        if (entry.BeginsConcentrationAfterLog && spell.DurationType == DurationType.Concentration)
            BeginConcentrationTracking(caster, null, spell);

        string combatEndContext = entry.CombatEndContext;
        if (combatEndContext == null && !req.HasQuirk(LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers))
            combatEndContext = entry.Name;
        if (combatEndContext != null && EvaluateCombatEnd(combatEndContext))
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.CombatEnd, combatEndContext);
            ctx.CombatEnded = true;
        }

        FinishCast(ctx, new CastOutcome(CastOutcomeKind.Resolved, CastStage.CombatEnd, ctx.CombatEnded ? "combat-over" : "resolved")
        {
            CombatEnded = ctx.CombatEnded
        });
    }

    // ── The Summon, Grease and HoldCharge resolve variants (batch 11 step 10) ───────────────────────────────────

    /// <summary>
    /// Resolve (summon): places the summoned creatures (Summon Monster and Summon Nature's Ally,
    /// <see cref="ResolveSummonMonsterCast"/>) or the swarm (Summon Swarm, <see cref="ResolveSummonSwarmCast"/>) on the
    /// aim's destination, inserts them into the initiative after the caster and registers them, then ends the cast at this
    /// stage (<see cref="FinishEffectCast"/>).
    /// </summary>
    private void CastStageResolveSummon(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Resolve);
        if (IsSummonSwarmSpell(ctx.Request.Spell))
            ResolveSummonSwarmCast(ctx);
        else
            ResolveSummonMonsterCast(ctx);
    }

    /// <summary>
    /// Resolve (Grease, PHB p.237): the mode the caster chose (CastChoices.GreaseMode) greases the aimed 10-ft square, the
    /// object the aim's holder carries or the armor it wears (<see cref="ResolveGreaseCast"/>); the cast ends with its
    /// result log (<see cref="FinishGreaseCast"/>). An object or armor carried by a creature inside a globe of
    /// invulnerability is a target within the globe, so the spell fails to affect it (PHB p.236): the cast ends Failed at
    /// the Globe stage with no effect (batch 11 step 11, SPL-141). The 10-ft square is a persistent area, not tested
    /// against globes (SPL-141's open part).
    /// </summary>
    private void CastStageResolveGrease(CastContext ctx)
    {
        if ((ctx.Request.Choices.GreaseMode == GreaseCastMode.Object || ctx.Request.Choices.GreaseMode == GreaseCastMode.Armor)
            && ctx.IsBlockedByGlobe(ctx.Request.Aim.ObjectHolder, out LesserGlobeOfInvulnerabilityAreaEffect blockingGlobe))
        {
            EnterCastStage(ctx, CastStage.Globe);
            CharacterController caster = ctx.Request.Caster;
            CharacterController holder = ctx.Request.Aim.ObjectHolder;
            SpellData spell = ctx.Request.Spell;
            CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "blocked");
            string casterName = caster.Stats != null ? caster.Stats.CharacterName : caster.name;
            string what = ctx.Request.Choices.GreaseMode == GreaseCastMode.Armor ? "armor" : "item";
            ctx.ResultLog = ctx.LogPrefix + CombatLogHelper.Defensive("🛡",
                $"{casterName} casts {spell.Name} at {holder.Stats.CharacterName}'s {what}, but {LesserGlobeOfInvulnerabilityAreaEffect.DescribeBlock(blockingGlobe)} around {holder.Stats.CharacterName} blocks it: the spell has no effect.");
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Globe, "globe-blocked"));
            return;
        }

        EnterCastStage(ctx, CastStage.Resolve);
        FinishGreaseCast(ctx, ResolveGreaseCast(ctx));
    }

    /// <summary>
    /// Resolve (hold): the cast spell's charge is held (SpellcastingComponent.SetHeldTouchCharge, PHB p.141 and p.176),
    /// with the cast's metamagic and the choices made at the hold (the energy type; batch 11 step 15), for a later
    /// delivery (OnDischargeHeldTouchButtonPressed, a HeldChargeDelivery cast that starts from a copy of them).
    /// </summary>
    private void CastStageResolveHoldCharge(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Resolve);
        CharacterController caster = ctx.Request.Caster;
        SpellData spell = ctx.Request.Spell;
        ctx.SpellComp.SetHeldTouchCharge(spell, ctx.Request.Metamagic, ctx.Request.Choices);
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, "hold-charge");
        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"✋ {caster.Stats.CharacterName} chooses Discharge Later and holds the charge of {spell.Name}."));
        FinishEffectCast(ctx, "held");
    }

    /// <summary>
    /// The exit of a resolved summon or hold: both logged their own lines as they resolved, so the cast has no result
    /// log; it ends at the Resolve stage, and the UI decider refreshes the stat panels, clears the highlights and pauses
    /// (AfterAttackDelay, 1 s). No combat-end check (<see cref="LegacyCastQuirk.EffectResolveSkipsCombatEndCheck"/>,
    /// CORE-039; step 11).
    /// </summary>
    private void FinishEffectCast(CastContext ctx, string reason)
    {
        if (ctx.Outcome != null)
            return;
        RequireLegacyQuirk(ctx, LegacyCastQuirk.EffectResolveSkipsCombatEndCheck);
        FinishCast(ctx, new CastOutcome(CastOutcomeKind.Resolved, CastStage.Resolve, reason));
    }

    /// <summary>
    /// The exit of a summon whose creature could not be spawned: the slot is spent and the cast is over, and the UI goes
    /// straight back to the action menu (<see cref="CastOutcome.ShowsActionMenu"/>), as the old summon paths did.
    /// </summary>
    private void FinishFailedSummon(CastContext ctx, string reason)
    {
        if (ctx.Outcome != null)
            return;
        FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Resolve, reason) { ShowsActionMenu = true });
    }

    /// <summary>
    /// The exit of a resolved Grease: its result log goes out, then the decider's follow-up that precedes a combat-end
    /// check (the UI's last combat log, stat panels and highlights), and the cast ends at the CombatEnd stage, so the UI
    /// paces the turn as after a resolved cast (AfterAttackDelay, 1.5 s). No combat-end check
    /// (<see cref="LegacyCastQuirk.EffectResolveSkipsCombatEndCheck"/>, CORE-039; step 11).
    /// </summary>
    private void FinishGreaseCast(CastContext ctx, string log)
    {
        if (ctx.Outcome != null)
            return;
        EnterCastStage(ctx, CastStage.CombatEnd);
        ctx.ResultLog = log;
        ctx.ConsoleLogLabel = null;
        FlushCastLog(ctx);
        ctx.Request.Decider?.BeforeCombatEndCheck(this, ctx);
        RequireLegacyQuirk(ctx, LegacyCastQuirk.EffectResolveSkipsCombatEndCheck);
        FinishCast(ctx, new CastOutcome(CastOutcomeKind.Resolved, CastStage.CombatEnd, "resolved"));
    }

    // ── The single exit ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The single exit of a run: shows the cast's result log once (if the run built one and it is not out yet), ends the
    /// context (EndCast), then calls the decider's finish continuation and the run's own.
    /// </summary>
    private void FinishCast(CastContext ctx, CastOutcome o)
    {
        if (ctx == null || ctx.Outcome != null)
            return;
        ctx.Outcome = o;

        FlushCastLog(ctx);
        EndCast(ctx);

        ctx.Request.Decider?.OnCastFinished(this, ctx, o);
        Action<CastOutcome> done = ctx.Done;
        ctx.Done = null;
        done?.Invoke(o);
    }

    /// <summary>Shows the cast's result log, once.</summary>
    private void FlushCastLog(CastContext ctx)
    {
        if (ctx.ResultLogShown || ctx.ResultLog == null)
            return;
        ctx.ResultLogShown = true;
        if (LogAttacksToConsole && ctx.Stage == CastStage.CombatEnd && ctx.ConsoleLogLabel != null)
            Debug.Log(ctx.ConsoleLogLabel + ctx.ResultLog);
        CombatUI?.ShowCombatLog(ctx.ResultLog);
    }

    // ── Stage helpers ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// True (with a warning and a cast-stack harness violation) when <paramref name="ctx"/>'s run is waiting for its
    /// decider's provocation answer: a second run would reset the first one's state (its spellcasting component, the
    /// slot to spend) and spend again. A run suspended at a choice dialog is not waiting (nothing past Legality ran),
    /// so its re-entry is the normal resume.
    /// </summary>
    private bool RejectCastReentry(CastContext ctx, string from)
    {
        if (ctx == null || !ctx.AwaitingDecider || ctx.Ended)
            return false;
        string detail = $"cast core: {from} re-entered {DescribeCast(ctx.Request)} while its run waits for the decider's provocation answer";
        Debug.LogWarning("[CastCore] " + detail);
        ScenarioHooks.CastStackViolation?.Invoke(detail);
        return true;
    }

    /// <summary>
    /// The outcome of a cast lost to a distraction or a spell failure: the slot is spent and the UI pauses as after any
    /// lost cast, on every path (batch 11 step 13 removed the summon and Grease paths' straight return to the action
    /// menu, SPL-006).
    /// </summary>
    private static CastOutcome LostCastOutcome(CastOutcomeKind kind, CastStage at, string reason)
    {
        return new CastOutcome(kind, at, reason);
    }

    /// <summary>
    /// A spell lost to a distraction or a spell failure still ends the caster's concentration, as every casting does
    /// (HandleConcentrationOnCasting; decision D1), for every caster (batch 11 step 15, SPL-114).
    /// </summary>
    private void CheckConcentrationOnLostCast(CastContext ctx)
    {
        EndConcentrationForCasting(ctx);
    }

    /// <summary>Spends the cast's slot (or scroll, wand charge, converted spell) with the parameters the Cost stage set.</summary>
    /// <remarks>The cast's priced material components go with it (PHB p.174: the casting process consumes them, and the XP
    /// Cost paragraph treats XP like a material component, expended whether or not the casting succeeds), on every path
    /// that spends a slot, a spell lost at the Distraction or Failure stage included (delegated RAW reading 2026-10-14;
    /// SpellComponentRules.SpendCostlyComponents, batch 11 step 17, SPL-020); the cancel snapshot records them for a
    /// refund.</remarks>
    private bool SpendCastSlot(CastContext ctx)
    {
        bool spent = ConsumePendingSpellSlot(ctx.SpellComp, ctx.Request.Spell, ctx.Request.Metamagic, ctx.HasMetamagicApplied,
            ctx.SlotLevelToConsume, ctx.IsSpontaneousConversion, ctx.SpontaneousLevel, ctx.SpontaneousSacrificedSpellId);
        if (spent)
            SpendCastCostlyComponents(ctx);
        return spent;
    }

    /// <summary>
    /// Spends the cast's priced material components after its slot (SpellComponentRules.SpendCostlyComponents; PHB p.174),
    /// logs them, and records them in the cancel snapshot so a cast cancelled at its AoO prompt gives them back.
    /// </summary>
    private void SpendCastCostlyComponents(CastContext ctx)
    {
        SpellCastRequest req = ctx.Request;
        var removed = new List<KeyValuePair<int, ItemData>>();
        string log = SpellComponentRules.SpendCostlyComponents(req.Caster, req.Spell, req.Source, removed, out int goldSpent);
        if (log == null)
            return;
        CastResourceSnapshot snapshot = PeekSpellcastResourceSnapshot();
        if (snapshot != null && snapshot.Caster == req.Caster)
        {
            if (snapshot.ComponentItemsSpent == null)
                snapshot.ComponentItemsSpent = new List<KeyValuePair<int, ItemData>>();
            snapshot.ComponentItemsSpent.AddRange(removed);
            snapshot.ComponentGoldSpent += goldSpent;
        }
        CastStageTrace.Mark(req.Caster, req.Spell, CastStageTrace.Spend, "costly-components");
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("💎", log));
    }

    /// <summary>
    /// After a spend that failed once the action was spent (no spellcasting component, or no slot, spell per day or
    /// sacrificed spell to spend): no spell was cast, so the snapshot is restored and the cast costs nothing (PHB p.141
    /// and p.143: casting the spell is the action; batch 11 step 14 removed SlotFailureClearsSnapshot, SPL-147).
    /// </summary>
    private void RefundAfterFailedSpend(CastContext ctx)
    {
        RestoreSpellcastResourceSnapshot(ctx.Request.Caster);
    }

    /// <summary>
    /// A stage that has only today's (legacy) behaviour for <paramref name="quirk"/>: a request without the bit raises a
    /// cast-core violation (a builder forgot to declare it; ScenarioHooks.CastStackViolation in the harness) and gets the
    /// legacy behaviour anyway, since the RAW one is not built yet.
    /// </summary>
    private static void RequireLegacyQuirk(CastContext ctx, LegacyCastQuirk quirk)
    {
        if (ctx.Request.HasQuirk(quirk))
            return;
        string detail = $"cast core: {DescribeCast(ctx.Request)} lacks the legacy quirk {quirk}, which stage {ctx.Stage} still needs";
        Debug.LogWarning("[CastCore] " + detail);
        ScenarioHooks.CastStackViolation?.Invoke(detail);
    }
}
