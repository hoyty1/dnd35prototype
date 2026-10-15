using System;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// The stages of the cast core (batch 11 step 7, docs/designs/cast_core_batch11.md), in the order a cast runs them
/// today. With <see cref="LegacyCastQuirk.GlobeAfterCast"/> (every cast so far) the Globe stage runs after Resolve,
/// and with <see cref="LegacyCastQuirk.ConcentrationGateBeforeProvocation"/> ConcentrationOnCast before Provocation.
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
    RingCounterspell,
    InvisibilityBreak,
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
/// PerformSpellCast's old body: every check, roll, log line and trace mark in today's order. The NPC path joins in step
/// 8, the area casts in step 9.
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
    /// Whether <paramref name="req"/> may be cast as it stands: the target test (IsValidTargetForSpell) and the
    /// material-component test (the component pouch, PHB p.174 and p.130; skipped for the F12 panel's Components bypass,
    /// scrolls and wands). No side effect. A refusal comes back with Kind Refused; a legal request with Kind Resolved
    /// and reason "ok". The caster's choices are not checked here (the decider makes them).
    /// </summary>
    internal CastOutcome CheckLegality(SpellCastRequest req)
    {
        CastOutcome refusal = CheckCastTargetLegality(req) ?? CheckCastComponentLegality(req);
        return refusal ?? new CastOutcome(CastOutcomeKind.Resolved, CastStage.Legality, "ok");
    }

    private CastOutcome CheckCastTargetLegality(SpellCastRequest req)
    {
        if (req == null || req.Spell == null || req.Caster == null)
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "no-cast");
        if (!IsValidTargetForSpell(req.Caster, req.Aim != null ? req.Aim.Target : null, req.Spell))
            return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "invalid-target");
        return null;
    }

    private static CastOutcome CheckCastComponentLegality(SpellCastRequest req)
    {
        // Spells with common material components (M with no GP cost) need a spell component pouch (PHB p.130). F12 test
        // panel casts (their Components bypass), scroll casts and wand casts bypass it (the item provides the
        // components); the cast's own source decides (batch 11 step 4).
        CastSource source = req.Source;
        bool componentsBypassed = source != null && (source.HasBypass(TestPanelBypass.Components) || source.IsItem);
        if (componentsBypassed || !req.Spell.HasMaterialComponent)
            return null;

        if (SpellComponentRegistry.ValidatePouchRequirement(req.Spell.SpellId, req.Spell, req.Caster, out string pouchFailure))
            return null;

        return new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "no-component-pouch") { Detail = pouchFailure };
    }

    // ── Stages ──────────────────────────────────────────────────────────────────────────────────────────────────

    private void EnterCastStage(CastContext ctx, CastStage stage)
    {
        ctx.Stage = stage;
        ctx.StageObserver?.Invoke(stage);
    }

    /// <summary>
    /// Legality: the target, then the caster's choices (the decider; the UI may suspend here at a dialog), then the
    /// components. Nothing is spent before the cast passes.
    /// </summary>
    private void CastStageLegality(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Legality);
        SpellCastRequest req = ctx.Request;
        CharacterController caster = req.Caster;
        SpellData spell = req.Spell;

        CastOutcome refusal = CheckCastTargetLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, "invalid-target");
            if (spell != null)
                CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("", $"{spell.Name} has invalid target (requires humanoid ally/enemy constraints)."));
            FinishCast(ctx, refusal);
            return;
        }

        CastChoiceFill fill = req.Decider.FillChoices(this, ctx);
        if (fill == CastChoiceFill.Suspended)
            return;
        if (fill == CastChoiceFill.Refused)
        {
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "choice-refused"));
            return;
        }

        refusal = CheckCastComponentLegality(req);
        if (refusal != null)
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Legality, "no-component-pouch");
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", $"{caster.Stats?.CharacterName} cannot cast {spell.Name} — requires a {refusal.Detail} for material components! Purchase one from the store (5 gp)."));
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

        CaptureSpellcastResourceSnapshot(caster);

        CastSourceKind sourceKind = req.Source.Kind;
        bool isDeliveringHeldCharge = sourceKind == CastSourceKind.HeldChargeDelivery;
        bool isScrollCast = sourceKind == CastSourceKind.Scroll;
        bool isWandCast = sourceKind == CastSourceKind.Wand;
        ctx.IsHeldChargeDelivery = isDeliveringHeldCharge;

        // Quickened applies when CASTING the spell, not when delivering a previously held charge.
        bool isQuickened = !isDeliveringHeldCharge && metamagic != null && metamagic.Has(MetamagicFeatId.QuickenSpell);
        ctx.IsQuickened = isQuickened;
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
        else if (!isQuickened)
        {
            // PHB p.88: spontaneous casters using metamagic take a full-round action (decided by the caster's class, SPL-159).
            bool hasAnyMetamagic = metamagic != null && metamagic.HasAnyMetamagic;
            if (hasAnyMetamagic)
                RequireLegacyQuirk(ctx, LegacyCastQuirk.SpontaneousFullRoundByCasterClass);
            bool isSpontaneousMetamagic = hasAnyMetamagic && MetamagicSystem.IsSpontaneousCaster(caster.Stats);
            if (isSpontaneousMetamagic)
                caster.Actions.UseFullRoundAction();
            else
                caster.CommitStandardAction();
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, isSpontaneousMetamagic ? "full-round" : "standard");
        }
        else
        {
            // Quicken is honoured before the spontaneous full-round rule (SPL-144). Mark that this character has used
            // their one quickened spell for this round.
            RequireLegacyQuirk(ctx, LegacyCastQuirk.QuickenBeforeSpontaneousFullRound);
            var casterSpellComp = caster.Spellcasting;
            if (casterSpellComp != null)
                casterSpellComp.MarkQuickenedSpellCast();
            CastStageTrace.Mark(caster, spell, CastStageTrace.Cost, "quickened");
        }
        // Test instrumentation (inert outside the scenario harness): a cast begins. Delivering a held charge is not a
        // cast (HoldPendingMeleeTouchCharge raised it when the spell was cast).
        if (!isDeliveringHeldCharge)
            ScenarioHooks.SpellCast?.Invoke(caster, target, spell);

        SpellcastingComponent spellComp = caster.Spellcasting;
        if (spellComp == null)
        {
            DropSnapshotAfterFailedSpend(ctx);
            Debug.LogError("[GameManager] PerformSpellCast: No SpellcastingComponent!");
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Cost, CastOutcome.ReasonNoSpellcasting));
            return;
        }
        ctx.SpellComp = spellComp;

        // A cleric converting a specific prepared spell: the cast's own source, which the spell menu filled once from its
        // Convert button (batch 11 step 4), so a cancelled conversion cannot make a later cast spontaneous.
        bool isSpontaneous = !isDeliveringHeldCharge && sourceKind == CastSourceKind.SpontaneousConversion;
        ctx.IsSpontaneousConversion = isSpontaneous;
        ctx.SpontaneousLevel = isSpontaneous ? req.Source.ConversionLevel : -1;
        ctx.SpontaneousSacrificedSpellId = isSpontaneous ? req.Source.SacrificedSpellId : null;

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

    /// <summary>Distraction: the entangled check, then grappled or pinned Concentration (not for a held-charge delivery).</summary>
    private void CastStageDistraction(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Distraction);
        if (!ctx.IsHeldChargeDelivery)
        {
            SpellCastRequest req = ctx.Request;
            CharacterController caster = req.Caster;
            SpellData spell = req.Spell;

            if (caster.HasCondition(CombatConditionType.Entangled))
                RequireLegacyQuirk(ctx, LegacyCastQuirk.EntangledUsesCasterLevelFormula);
            if (!ResolveEntangledSomaticCastingConcentration(caster, ctx.SpellComp, spell, req.Metamagic, ctx.HasMetamagicApplied,
                    ctx.SlotLevelToConsume, ctx.IsSpontaneousConversion, ctx.SpontaneousLevel, ctx.SpontaneousSacrificedSpellId))
            {
                HandleConcentrationOnCasting(caster, spell);
                ClearSpellcastResourceSnapshot();
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Interrupted, CastStage.Distraction, "entangled"));
                return;
            }

            if (!ResolveGrappledOrPinnedCastingConcentration(caster, ctx.SpellComp, spell, req.Metamagic, ctx.HasMetamagicApplied,
                    ctx.SlotLevelToConsume, ctx.IsSpontaneousConversion, ctx.SpontaneousLevel, ctx.SpontaneousSacrificedSpellId))
            {
                HandleConcentrationOnCasting(caster, spell);
                ClearSpellcastResourceSnapshot();
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Interrupted, CastStage.Distraction, "grappled"));
                return;
            }
        }

        CastStageFailure(ctx);
    }

    /// <summary>Failure: arcane spell failure, then the blinking caster's 20% (not for a held-charge delivery). A failed spell spends its slot.</summary>
    private void CastStageFailure(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Failure);
        if (!ctx.IsHeldChargeDelivery)
        {
            SpellCastRequest req = ctx.Request;
            CharacterController caster = req.Caster;
            SpellData spell = req.Spell;

            if (req.Source.Kind == CastSourceKind.Scroll)
                RequireLegacyQuirk(ctx, LegacyCastQuirk.ScrollSkipsArcaneFailure);
            if (TryRollArcaneSpellFailure(caster, spell, false, out int asfRoll, out int asfChance))
            {
                if (!SpendCastSlot(ctx))
                {
                    DropSnapshotAfterFailedSpend(ctx);
                    Debug.LogError($"[GameManager] ASF failure path: failed to consume level {ctx.SlotLevelToConsume} spell slot!");
                    FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Failure, CastOutcome.ReasonSlotNotSpent));
                    return;
                }

                HandleConcentrationOnCasting(caster, spell);
                LogArcaneSpellFailure(caster, spell, asfRoll, asfChance);
                ClearSpellcastResourceSnapshot();
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Failure, "arcane-spell-failure"));
                return;
            }

            // PHB p.206: a blinking caster has a 20% spell failure chance (the caster may be on the Ethereal Plane when
            // the spell is cast), checked separately from and in addition to arcane spell failure.
            if (caster.HasActiveBlinkEffect)
            {
                int blinkSpellRoll = DiceService.Percentile("Blink caster spell failure");
                CastStageTrace.Mark(caster, spell, CastStageTrace.Failure, blinkSpellRoll <= 20 ? "blink-failed" : "blink-passed");
                if (blinkSpellRoll <= 20)
                {
                    if (!SpendCastSlot(ctx))
                    {
                        DropSnapshotAfterFailedSpend(ctx);
                        Debug.LogError($"[GameManager] Blink spell failure path: failed to consume level {ctx.SlotLevelToConsume} spell slot!");
                        FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Failure, CastOutcome.ReasonSlotNotSpent));
                        return;
                    }

                    HandleConcentrationOnCasting(caster, spell);
                    CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡", $"{caster.Stats.CharacterName}'s {spell.Name} fizzles! (Blink spell failure: rolled {blinkSpellRoll} ≤ 20%)"));
                    ClearSpellcastResourceSnapshot();
                    FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Failure, "blink-caster"));
                    return;
                }

                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡", $"Blink spell check: {caster.Stats.CharacterName} rolled {blinkSpellRoll} > 20% (spell proceeds)."));
            }
        }

        CastStageSpend(ctx);
    }

    /// <summary>Spend: the slot (or the scroll, the wand charge, the converted spell), not for a held-charge delivery.</summary>
    private void CastStageSpend(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Spend);
        if (!ctx.IsHeldChargeDelivery)
        {
            CharacterController caster = ctx.Request.Caster;
            SpellData spell = ctx.Request.Spell;
            if (!SpendCastSlot(ctx))
            {
                DropSnapshotAfterFailedSpend(ctx);
                Debug.LogError($"[GameManager] Failed to consume level {ctx.SlotLevelToConsume} spell slot!");
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.Spend, CastOutcome.ReasonSlotNotSpent));
                return;
            }

            if (ctx.IsSpontaneousConversion && !string.IsNullOrEmpty(ctx.SpontaneousSacrificedSpellId))
                Debug.Log($"[GameManager] Spontaneous cast: {caster.Stats.CharacterName} sacrificed '{ctx.SpontaneousSacrificedSpellId}' → {spell.Name}");
            else if (ctx.IsSpontaneousConversion)
                Debug.Log($"[GameManager] Spontaneous cast (level-based): {caster.Stats.CharacterName} converted a level {ctx.SpontaneousLevel} slot → {spell.Name}");
        }

        if (ctx.Request.HasQuirk(LegacyCastQuirk.ConcentrationGateBeforeProvocation))
            CastStageConcentrationOnCast(ctx, CastStageProvocation);
        else
            CastStageProvocation(ctx);
    }

    /// <summary>
    /// ConcentrationOnCast: a caster concentrating on another spell checks it as it casts (HandleConcentrationOnCasting;
    /// decision D1 replaces it in step 16). Before the provocation stage with
    /// <see cref="LegacyCastQuirk.ConcentrationGateBeforeProvocation"/> (SPL-152), else once the provocation lets the cast proceed.
    /// </summary>
    private void CastStageConcentrationOnCast(CastContext ctx, Action<CastContext> next)
    {
        EnterCastStage(ctx, CastStage.ConcentrationOnCast);
        HandleConcentrationOnCasting(ctx.Request.Caster, ctx.Request.Spell);
        next(ctx);
    }

    /// <summary>
    /// Provocation: a threatened cast provokes attacks of opportunity (PHB p.140); the decider chooses to cast
    /// defensively, cast normally or cancel and resolves the rolls. No provocation for a held-charge delivery, nor (without
    /// <see cref="LegacyCastQuirk.QuickenedProvokes"/>, SPL-143) for a quickened cast.
    /// </summary>
    private void CastStageProvocation(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Provocation);
        bool provokes = !ctx.IsHeldChargeDelivery
                        && (!ctx.IsQuickened || ctx.Request.HasQuirk(LegacyCastQuirk.QuickenedProvokes));
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
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Interrupted, CastStage.Provocation, "lost"));
            return;
        }

        if (ctx.Request.HasQuirk(LegacyCastQuirk.ConcentrationGateBeforeProvocation))
            CastStageCounterspell(ctx);
        else
            CastStageConcentrationOnCast(ctx, CastStageCounterspell);
    }

    /// <summary>Counterspell: a readied counterspell (PHB p.170) stops the spell; not for a held-charge delivery.</summary>
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

        CastStageRingCounterspell(ctx);
    }

    /// <summary>RingCounterspell: a Ring of Counterspells on the target holding this spell counters it (DMG p.230).</summary>
    private void CastStageRingCounterspell(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.RingCounterspell);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;
        if (target != null && spell != null && CounterspellManager.TryRingCounterspell(target, spell))
        {
            CastStageTrace.Mark(caster, spell, CastStageTrace.Ring, "countered");
            Debug.Log($"[RingCounterspell] {spell.Name} countered by Ring of Counterspells on {target.Stats?.CharacterName}!");
            FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.RingCounterspell, "ring"));
            return;
        }

        CastStageInvisibilityBreak(ctx);
    }

    /// <summary>
    /// InvisibilityBreak: a hostile cast ends the caster's invisibility (PHB p.245). Here too, as before, the cast settles
    /// whether a willing target skips the touch attack and whether the target forgoes its save.
    /// </summary>
    private void CastStageInvisibilityBreak(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.InvisibilityBreak);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;

        BreakInvisibilityOnHostileSpellCast(caster, spell, target, null);
        // A willing friendly target of a melee touch spell needs no touch attack.
        ctx.SkipFriendlyTouchAttackRoll = spell.IsMeleeTouchSpell() && IsFriendlyTarget(caster, target);
        ctx.TargetForgoesSave = ShouldForceTargetToAcceptSave(caster, target, spell);
        if (ctx.TargetForgoesSave)
            RequireLegacyQuirk(ctx, LegacyCastQuirk.AlliesForgoEverySave);

        CastStageMirrorImage(ctx);
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
    /// for area or self spells.
    /// </summary>
    private void CastStageTargetBlink(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.TargetBlink);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;
        if (target != null && target != caster && target.HasActiveBlinkEffect
            && spell.TargetType != SpellTargetType.Self
            && spell.TargetType != SpellTargetType.Area)
        {
            int blinkTargetRoll = DiceService.Percentile("Blink target spell failure");
            CastStageTrace.Mark(caster, spell, CastStageTrace.TargetBlink, blinkTargetRoll <= 50 ? "missed" : "connects");
            string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
            if (blinkTargetRoll <= 50)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{spell.Name} fails to reach {targetName}! Target is on the Ethereal Plane. (Blink: rolled {blinkTargetRoll} ≤ 50%)"));
                FinishCast(ctx, new CastOutcome(CastOutcomeKind.Failed, CastStage.TargetBlink, "target-blinked"));
                return;
            }

            CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"Blink target check: {targetName} rolled {blinkTargetRoll} > 50% (spell connects)."));
        }

        // The globe is still tested after SpellCaster.Cast (SPL-141): no pre-cast globe stage yet.
        RequireLegacyQuirk(ctx, LegacyCastQuirk.GlobeAfterCast);
        CastStageResolve(ctx);
    }

    /// <summary>
    /// Resolve: SpellCaster.Cast (attack roll, spell resistance, save, generic damage or healing), then the save's and
    /// the mind-affecting immunity's log lines; the Globe stage follows (<see cref="LegacyCastQuirk.GlobeAfterCast"/>).
    /// </summary>
    private void CastStageResolve(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Resolve);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;

        if (caster.HasCondition(CombatConditionType.Deafened))
            RequireLegacyQuirk(ctx, LegacyCastQuirk.DeafenedRolledInSpellCaster);
        SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, ctx.Request.Metamagic, ctx.SkipFriendlyTouchAttackRoll, ctx.TargetForgoesSave, caster, target);
        ctx.Result = result;
        CastStageTrace.Mark(caster, spell, CastStageTrace.SpellCasterCast, result.Success ? "success" : "failed");

        // The landed cast's effect step (ApplySpellBuff): every Buff, Debuff, Control, Illusion and Wall spell, and the
        // other spells ApplySpellBuff has a branch for. Shared with TryNPCPerformSpellCast (SPL-037).
        ctx.AppliesTrackedEffect = SpellEffectRouting.ReachesApplySpellBuff(spell);

        // Shared with TryNPCPerformSpellCast (SPL-007): Debuff and Control saves negate; Cause Fear and Scare are
        // partial; nonintelligent undead get no save vs Command Undead.
        bool effectNegatedBySave = SpellUtilities.IsEffectNegatedBySave(spell, result, target != null && !target.IsIntelligentUndead());
        ctx.EffectNegatedBySave = effectNegatedBySave;
        if (effectNegatedBySave)
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{target.Stats.CharacterName} resists {spell.Name} with a successful {result.SaveType} save."));

        if (result.MindAffectingImmunityBlocked)
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧠", $"{target.Stats.CharacterName} is immune to mind-affecting effects. {spell.Name} has no effect."));

        CastStageGlobe(ctx);
    }

    /// <summary>
    /// Globe: a lesser globe (3rd level or lower) or globe of invulnerability (4th or lower) around the target blocks the
    /// spell (PHB p.236); the caster's own spells too. Today after SpellCaster.Cast, at the base level (SPL-141).
    /// </summary>
    private void CastStageGlobe(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Globe);
        CharacterController caster = ctx.Request.Caster;
        CharacterController target = ctx.Request.Aim.Target;
        SpellData spell = ctx.Request.Spell;
        SpellResult result = ctx.Result;
        if (target != null && spell != null && result.Success && !ctx.EffectNegatedBySave)
        {
            if (LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(spell, target, out LesserGlobeOfInvulnerabilityAreaEffect blockingGlobe))
            {
                result.Success = false;
                CastStageTrace.Mark(caster, spell, CastStageTrace.Globe, "blocked");
                CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{spell.Name} (level {spell.SpellLevel}) is blocked by {LesserGlobeOfInvulnerabilityAreaEffect.DescribeBlock(blockingGlobe)} around {target.Stats.CharacterName}."));
            }
        }

        CastStageHandlers(ctx);
    }

    /// <summary>
    /// Handlers: the SpellId registry (GameManager.SpellHandlerRegistry.cs) runs the handler registered for the spell when
    /// its gate passes. A claimed cast skips the Buff stage; Shield Other and the Sound Burst stun go on to it.
    /// </summary>
    private void CastStageHandlers(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.Handlers);
        SpellData spell = ctx.Request.Spell;
        if (string.Equals(spell.SpellId, SpellNames.SOUND_BURST, StringComparison.Ordinal))
            RequireLegacyQuirk(ctx, LegacyCastQuirk.SoundBurstStunIgnoresSuccess);
        else if (string.Equals(spell.SpellId, SpellNames.ANIMATE_ROPE, StringComparison.Ordinal))
            RequireLegacyQuirk(ctx, LegacyCastQuirk.AnimateRopeIgnoresSuccess);

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

            if (appliedEffect != null && spell.DurationType == DurationType.Concentration)
                BeginConcentrationTracking(caster, appliedEffect, spell);
        }

        CastStagePostCast(ctx);
    }

    /// <summary>
    /// PostCast: the target's concentration against the spell's damage, the held charge (kept when the delivering touch
    /// misses, PHB p.141), the target's death, and the cast's result log with its prefixes.
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

        if (result.TargetKilled && target != null)
        {
            target.OnDeath();
            HandleSummonDeathCleanup(target);
        }

        string log = ctx.LogPrefix + result.GetFormattedLog();
        if (ctx.RetainedHeldChargeOnMiss)
            log += $"\n✋ Touch attack missed — {caster.Stats.CharacterName} retains {spell.Name} charge.";
        ctx.ResultLog = log;

        CastStageCombatEnd(ctx);
    }

    /// <summary>
    /// CombatEnd: the result log goes out first, then the decider's follow-up that precedes the check (the UI's stat
    /// panels, which also refresh the Flanked conditions, and highlights, in the old order), then the shared combat-end
    /// check (EvaluateCombatEnd), also when a handler dealt the damage through DealDamage without setting
    /// result.TargetKilled (SPL-004). With <see cref="LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath"/> (CORE-039)
    /// only when the target was killed or is out of the fight.
    /// </summary>
    private void CastStageCombatEnd(CastContext ctx)
    {
        EnterCastStage(ctx, CastStage.CombatEnd);
        FlushCastLog(ctx);
        ctx.Request.Decider?.BeforeCombatEndCheck(this, ctx);

        CharacterController target = ctx.Request.Aim.Target;
        SpellResult result = ctx.Result;
        bool check = !ctx.Request.HasQuirk(LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath)
                     || result.TargetKilled || (target != null && CombatEndRules.IsOutOfFight(target));
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
        if (LogAttacksToConsole && ctx.Stage == CastStage.CombatEnd)
            Debug.Log("[Spell] " + ctx.ResultLog);
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

    /// <summary>Spends the cast's slot (or scroll, wand charge, converted spell) with the parameters the Cost stage set.</summary>
    private bool SpendCastSlot(CastContext ctx)
    {
        return ConsumePendingSpellSlot(ctx.SpellComp, ctx.Request.Spell, ctx.Request.Metamagic, ctx.HasMetamagicApplied,
            ctx.SlotLevelToConsume, ctx.IsSpontaneousConversion, ctx.SpontaneousLevel, ctx.SpontaneousSacrificedSpellId);
    }

    /// <summary>
    /// After a spend that failed once the action was spent: with <see cref="LegacyCastQuirk.SlotFailureClearsSnapshot"/>
    /// the snapshot is dropped and the action lost (SPL-147), else it is restored.
    /// </summary>
    private void DropSnapshotAfterFailedSpend(CastContext ctx)
    {
        if (ctx.Request.HasQuirk(LegacyCastQuirk.SlotFailureClearsSnapshot))
            ClearSpellcastResourceSnapshot();
        else
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
