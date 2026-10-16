using System;

/// <summary>
/// The state of one open cast (batch 11, cast_core_batch11.md): its <see cref="SpellCastRequest"/> and the fields the
/// staged cast core (GameManager.ExecuteCast, Spell/Resolution/GameManager.CastCore.cs) fills as the cast runs.
/// GameManager keeps open contexts on a stack (BeginCast and EndCast, Spell/Resolution/GameManager.CastContext.cs): a
/// synchronous AI cast nests on top of a Ui cast that waits for the player, and a pop that does not match the top is a
/// scenario-harness violation.
/// </summary>
public sealed class CastContext
{
    public readonly SpellCastRequest Request;

    /// <summary>The cast's SpellCaster result, once resolved.</summary>
    public SpellResult Result;

    /// <summary>Whether a save negated the effect.</summary>
    public bool EffectNegatedBySave;

    /// <summary>The caster level and save DC the cast uses (filled from batch 11 step 14, CasterLevelRules).</summary>
    public int CasterLevel;
    public int SaveDC;

    /// <summary>
    /// The quickened and spontaneous prefixes of the cast's result log, built once by the Cost stage (SPL-063): the
    /// quickened line, then the spontaneous line, each ending in a newline; empty for a plain cast.
    /// </summary>
    public string LogPrefix = "";

    /// <summary>The actions and spells per day before the spend, for a cancel to give back (moves here with the cast core).</summary>
    internal CastResourceSnapshot Snapshot;

    /// <summary>Set when the context left the cast stack (EndCast, or a combat reset).</summary>
    public bool Ended { get; internal set; }

    /// <summary>The stage the cast core is running or ran last.</summary>
    public CastStage Stage { get; internal set; }

    /// <summary>The outcome FinishCast gave the cast, or null while it runs or waits for the player.</summary>
    public CastOutcome Outcome { get; internal set; }

    /// <summary>
    /// Called with each stage as the cast core enters it (a static test records the sequence). Null in play: the core
    /// then does nothing extra.
    /// </summary>
    public Action<CastStage> StageObserver;

    // ── The current run's state (GameManager.CastCore.cs); a run that re-enters after a dialog starts it again ──

    /// <summary>The continuation ExecuteCast was given; FinishCast calls it once.</summary>
    internal Action<CastOutcome> Done;

    /// <summary>
    /// Set while the run waits for the decider's provocation answer (the PC's AoO prompt). ExecuteCast refuses to start
    /// another run of this context meanwhile (GameManager.RejectCastReentry); ResetRun leaves it alone.
    /// </summary>
    internal bool AwaitingDecider;

    internal bool IsHeldChargeDelivery;
    /// <summary>
    /// Set once the run has ended the caster's concentration for this casting (decision D1, batch 11 step 16): at the
    /// provocation, before its rolls, or at the ConcentrationOnCast stage, so the step runs once per run.
    /// </summary>
    internal bool ConcentrationHandled;
    internal bool IsQuickened;
    /// <summary>A quickened cast or a free-action spell (Feather Fall): the round's quickened spell, no provocation (CastActionRules).</summary>
    internal bool CountsAsQuickenedSpell;
    internal bool IsSpontaneousConversion;
    internal int SpontaneousLevel = -1;
    internal string SpontaneousSacrificedSpellId;
    internal bool HasMetamagicApplied;
    internal int SlotLevelToConsume;
    internal SpellcastingComponent SpellComp;
    internal bool SkipFriendlyTouchAttackRoll;
    /// <summary>
    /// Whether the single-target cast's target gives up its save (<see cref="TargetForgoesSave"/>, asked at the
    /// InvisibilityBreak stage; SpellCaster.Cast then records a failed save).
    /// </summary>
    internal bool ForgoesSingleTargetSave;
    internal bool AppliesTrackedEffect;
    internal bool ClaimedByHandler;
    internal bool RetainedHeldChargeOnMiss;
    internal bool CombatEnded;

    /// <summary>
    /// The globe of invulnerability around the target of a melee touch spell it excludes, or null: the touch is still
    /// made, and a touch that lands discharges the spell with no effect (GameManager.CastStageGlobe, SpellCaster.Cast;
    /// delegated RAW reading 2026-10-14, SPL-141).
    /// </summary>
    internal LesserGlobeOfInvulnerabilityAreaEffect GlobeAgainstTouch;

    /// <summary>The cast's result log (with <see cref="LogPrefix"/>), shown once (GameManager.FlushCastLog).</summary>
    internal string ResultLog;
    internal bool ResultLogShown;

    /// <summary>
    /// The console label FlushCastLog puts before the result log when GameManager.LogAttacksToConsole is on, or null for
    /// none (an area handler's log; the generic area loop's is "[AoE Spell] ").
    /// </summary>
    internal string ConsoleLogLabel = "[Spell] ";

    /// <summary>The area handler resolving an area cast (GameManager.SpellHandlerRegistry.cs, batch 11 step 9), once chosen.</summary>
    internal AreaHandlerEntry AreaHandler;

    public CastContext(SpellCastRequest request)
    {
        Request = request ?? new SpellCastRequest();
    }

    /// <summary>
    /// Whether a lesser globe or globe of invulnerability keeps this cast's spell from affecting <paramref name="target"/>
    /// (PHB p.236; LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell, by the spell's base level), and which
    /// globe does. The cast core's Globe stages test it before any effect (batch 11 step 11, SPL-141); the single-target
    /// stage lets a targeted Dispel Magic, a melee touch and the spells whose own handler cuts the globe out of their burst
    /// through (GameManager.CastStageGlobe).
    /// </summary>
    public bool IsBlockedByGlobe(CharacterController target, out LesserGlobeOfInvulnerabilityAreaEffect blockingGlobe)
    {
        blockingGlobe = null;
        return target != null && Request.Spell != null
               && LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(Request.Spell, target, out blockingGlobe);
    }

    /// <summary>
    /// Whether <paramref name="target"/> gives up its save against this cast (PHB p.177; batch 11 step 18): its own decider
    /// answers (SpellSaveRules.TargetForgoesSave, ICastDecider.ForgoSave), yes only against a harmless or beneficial
    /// spell of its own side.
    /// </summary>
    public bool TargetForgoesSave(CharacterController target)
        => SpellSaveRules.TargetForgoesSave(Request.Caster, target, Request.Spell);

    /// <summary>
    /// <paramref name="target"/>'s save against this cast's spell (SpellSaveRules.RollSave): given up when
    /// <see cref="TargetForgoesSave"/>, else one d20 with SaveRules.Modifier for the spell-and-caster context (SPL-018).
    /// </summary>
    public SaveResult RollSave(CharacterController target, SaveType save, int dc, string diceContext = null, int extraModifier = 0)
        => SpellSaveRules.RollSave(Request.Caster, target, Request.Spell, save, dc, diceContext, extraModifier);

    /// <summary>
    /// The caster level check against <paramref name="target"/>'s spell resistance (PHB p.177): d20 + the cast's caster
    /// level (CasterLevelRules.For, a scroll's or wand's own) + Spell Penetration; skipped when it has none (SPL-122).
    /// </summary>
    public SRResult RollSpellResistance(CharacterController target, string diceContext = null)
        => SpellSaveRules.RollSpellResistance(Request.Caster, target, Request.Spell, diceContext, CasterLevelRules.For(Request));

    /// <summary>Clears the run state before a run of the cast core (a cast suspended at a dialog runs again).</summary>
    internal void ResetRun()
    {
        Result = null;
        EffectNegatedBySave = false;
        LogPrefix = "";
        Outcome = null;
        Done = null;
        IsHeldChargeDelivery = false;
        ConcentrationHandled = false;
        IsQuickened = false;
        CountsAsQuickenedSpell = false;
        IsSpontaneousConversion = false;
        SpontaneousLevel = -1;
        SpontaneousSacrificedSpellId = null;
        HasMetamagicApplied = false;
        SlotLevelToConsume = 0;
        SpellComp = null;
        SkipFriendlyTouchAttackRoll = false;
        ForgoesSingleTargetSave = false;
        AppliesTrackedEffect = false;
        ClaimedByHandler = false;
        RetainedHeldChargeOnMiss = false;
        CombatEnded = false;
        GlobeAgainstTouch = null;
        ResultLog = null;
        ResultLogShown = false;
        ConsoleLogLabel = "[Spell] ";
        AreaHandler = null;
    }
}
