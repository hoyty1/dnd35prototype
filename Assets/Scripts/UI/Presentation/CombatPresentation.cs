using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How the presentation layer shows what the rules did. Step 8 builds Passthrough only; step 9 adds Holding.</summary>
public enum PresentationMode
{
    /// <summary>Every facade call applies at once, in the same order and the same frame as before the facade.</summary>
    Passthrough,
}

/// <summary>
/// The class of a public <see cref="CombatUI"/> method (docs/designs/grouped_ai_turns.md "`CombatUI` method classes"):
/// how the facade treats it while a group is recorded (step 9 on). In Passthrough every class applies at once.
/// </summary>
public enum UiCallClass
{
    /// <summary>A value beat: recorded with its call-time values while queueing, applied at once otherwise.</summary>
    Queued,
    /// <summary>The control beat (the action buttons show, S1): a boundary, then the show.</summary>
    Control,
    /// <summary>An interactive barrier (a defender prompt, the defeat panel): a boundary, then the show.</summary>
    Interactive,
    /// <summary>Any other prompt, menu or picker: catch up first if it is ever called while the queue is behind (step 10).</summary>
    CatchUp,
    /// <summary>Queries, hides, ensures, out-of-combat panels and PC-turn controls: always at once.</summary>
    Live,
}

/// <summary>A rules boundary at which the token keyframes are taken (CombatPresentation.Boundary).</summary>
public enum PresentationBoundary
{
    /// <summary>CombatPacing.Wait.</summary>
    Wait,
    /// <summary>A CharacterController.MoveAlongPath step (CombatPresentation.RecordStep).</summary>
    Step,
    /// <summary>Before the control beat (CombatUI.ShowActionButtonsForCharacter).</summary>
    Control,
    /// <summary>Before an Interactive-class call (CombatUI.ShowConfirmationDialog, CombatUI.ShowDefeatPanel).</summary>
    Interactive,
    /// <summary>TurnControl.CombatEnded.</summary>
    CombatEnd,
}

/// <summary>
/// The presentation layer of grouped AI turns (owner request 2026-10-30; docs/designs/grouped_ai_turns.md
/// "Recording (resolve now, show later)", "Playback", "Drains and clears"). Step 8 (gt-presentation-gates) builds it in
/// Passthrough only, a PURE refactor: the <see cref="CombatUI"/> facade (its Queued, Control and Interactive classes),
/// the SquareCell highlight gate, the PersistentAreaEffect indicator hooks and <see cref="Defer"/> for one-shot visuals
/// all route through here and every call still applies at once, in the same order and the same frame. Holding (the
/// recording of a group), the playback loop and the drains come with step 9.
///
/// <see cref="Boundary"/> is called at every rules boundary a group would have (each CombatPacing.Wait, each
/// MoveAlongPath step, before the control beat and every Interactive-class call, and in TurnControl.CombatEnded). In
/// Passthrough it only feeds <see cref="PresentationTrace"/> (the token keyframes; editor and development builds, while
/// the scenario harness records), never changes a returned yield, and costs one bool read otherwise.
///
/// The singleton (<see cref="Instance"/>, for step 9's playback loop) is a MonoBehaviour on its own lazily created root
/// GameObject, never on GameManager: GameManager.Awake runs before scene references are assigned (CORE-009) and
/// ResetCombatStateForNextEncounter stops GameManager's coroutines (CORE-013), so a reset never stops the playback
/// silently; <see cref="Clear"/> is explicit.
///
/// Presentation only: nothing here draws from UnityEngine.Random, writes rules state, or calls SetActive or Destroy on a
/// model (a source scan in Tests.UI.CombatPresentationTests pins it).
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatPresentation : MonoBehaviour
{
    /// <summary>The name of the singleton's root GameObject.</summary>
    public const string RootName = "CombatPresentation";

    private static CombatPresentation _instance;
    private static readonly List<PresentationBeat> _queue = new List<PresentationBeat>();
    private static readonly List<Action> _whenDrained = new List<Action>();
    private static int _seq;

    /// <summary>
    /// A test seam (Tests.UI.CombatPresentationTests, the trace fault check): when set, it replaces TokenKeyframe.Capture
    /// at a boundary. Null outside that test.
    /// </summary>
    internal static Func<TokenView, TokenKeyframe> KeyframeSourceForTests;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _queue.Clear();
        _whenDrained.Clear();
        _seq = 0;
        KeyframeSourceForTests = null;
    }

    /// <summary>
    /// The singleton, created on first use on its own root GameObject (<see cref="RootName"/>); null outside Play mode.
    /// Nothing in Passthrough needs it: step 9's playback loop runs on it.
    /// </summary>
    public static CombatPresentation Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                var go = new GameObject(RootName);
                _instance = go.AddComponent<CombatPresentation>();
            }
            return _instance;
        }
    }

    /// <summary>True when the singleton exists (no creation).</summary>
    public static bool HasInstance => _instance != null;

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>The mode in force: Passthrough (step 8).</summary>
    public static PresentationMode Mode => PresentationMode.Passthrough;

    /// <summary>True while a group is recorded (step 9). Always false in Passthrough.</summary>
    public static bool Holding => false;

    /// <summary>
    /// True while facade calls are recorded instead of applied: while holding, or while the queue is not empty
    /// (queue while behind keeps the order across a group close). Never true in Passthrough, where nothing is queued.
    /// </summary>
    public static bool Queueing => Holding || _queue.Count > 0;

    /// <summary>True while world input, the I/K/C toggles and End Turn must wait for the playback (wired in step 9).</summary>
    public static bool BlocksInput => Queueing;

    /// <summary>True while an interactive beat waits in the queue (Harness_PendingPrompt joins it in step 10).</summary>
    public static bool HasPendingInteractive
    {
        get
        {
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i].Kind == BeatKind.Interactive)
                    return true;
            return false;
        }
    }

    /// <summary>The beats waiting to be applied.</summary>
    public static int PendingBeats => _queue.Count;

    /// <summary>The actions waiting for the queue to drain (<see cref="WhenDrained"/>).</summary>
    public static int PendingDrainActions => _whenDrained.Count;

    /// <summary>
    /// Records a beat in rules order. Only called while <see cref="Queueing"/>; in Passthrough nothing calls it but the
    /// tests, which empty the queue again.
    /// </summary>
    internal static void Enqueue(PresentationBeat beat)
    {
        if (beat == null)
            return;
        beat.Seq = ++_seq;
        _queue.Add(beat);
    }

    // ── Boundaries and the trace ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A rules boundary: the token keyframes are taken here. In Passthrough this feeds only <see cref="PresentationTrace"/>
    /// while it records (editor and development builds, the scenario harness); otherwise it returns at once. It never
    /// changes the caller's yield, and reads the tokens only (TokenKeyframe.Capture).
    /// </summary>
    public static void Boundary(PresentationBoundary kind)
    {
        if (!PresentationTrace.Recording)
            return;
        // Trace only, inside rules coroutines: a throw here (a keyframe of a half-reset creature, say) must never reach
        // the caller, or the rules outcome would depend on whether the harness records. The trace faults and stops.
        try
        {
            PresentationTrace.BoundaryReached(kind);
            IReadOnlyList<TokenView> views = TokenView.Live;
            for (int i = 0; i < views.Count && PresentationTrace.Recording; i++)
            {
                TokenView view = views[i];
                if (view == null || view.IsReleased || view.IsDespawning || view.Model == null)
                    continue;
                TokenKeyframe kf = KeyframeSourceForTests != null ? KeyframeSourceForTests(view) : TokenKeyframe.Capture(view);
                PresentationTrace.Keyframe(view, kf);
            }
        }
        catch (Exception e)
        {
            PresentationTrace.Fault(e, "boundary " + kind);
        }
    }

    /// <summary>
    /// A MoveAlongPath step, called right after the mover's GridPosition becomes <paramref name="to"/> (before its
    /// tween): a boundary (the keyframes show the mover where the step starts) and, while the trace records, the step.
    /// Holding (step 9) records it as a step beat; the rules half of the step is untouched.
    /// </summary>
    public static void RecordStep(CharacterController mover, Vector2Int from, Vector2Int to, Vector3 startPos, Vector3 endPos, float secondsPerStep)
    {
        if (!PresentationTrace.Recording)
            return;
        try
        {
            Boundary(PresentationBoundary.Step);
            PresentationTrace.Step(mover != null ? mover.View : null, mover, from, to);
        }
        catch (Exception e)
        {
            PresentationTrace.Fault(e, "step");
        }
    }

    // ── One-shot visuals, area indicators, drains ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows a one-shot visual (a closure over values taken at call time, never over live rules state): at once in
    /// Passthrough, as a Deferred beat while queueing. <paramref name="label"/> names it in the trace.
    /// </summary>
    public static void Defer(Action show, string label)
    {
        if (show == null)
            return;
        if (Queueing)
        {
            Enqueue(new PresentationBeat { Kind = BeatKind.Deferred, Show = show, Text = label });
            return;
        }
        PresentationTrace.Applied("defer", label);
        show();
    }

    /// <summary>
    /// A persistent area's indicator was created (PersistentAreaEffect.CreateVisualIndicator). In Passthrough it shows
    /// at once, as before; Holding (step 9) hides its renderers until its AreaShown beat.
    /// </summary>
    public static void AreaShown(PersistentAreaEffect effect, GameObject indicator)
    {
        if (indicator == null)
            return;
        if (PresentationTrace.Recording)
            PresentationTrace.Applied("area", "+" + AreaLabel(effect));
    }

    /// <summary>
    /// A persistent area's indicator goes (PersistentAreaEffect.ReleaseIndicator: its expiry or a re-creation, in rules
    /// order; OnDestroy is the fallback of the direct Destroy sites). Returns true when the presentation took the
    /// indicator over (Holding, step 9: the caller detaches it and the presentation destroys it at its beat); false in
    /// Passthrough, where the caller destroys it at once as before.
    /// </summary>
    public static bool AreaHidden(PersistentAreaEffect effect, GameObject indicator)
    {
        if (indicator == null)
            return false;
        if (PresentationTrace.Recording)
            PresentationTrace.Applied("area", "-" + AreaLabel(effect));
        return false;
    }

    private static string AreaLabel(PersistentAreaEffect effect)
    {
        if (effect == null)
            return "?";
        Vector2Int cell = SquareGridUtils.WorldToGrid(effect.CenterPosition);
        return effect.EffectName + "@" + cell.x + "," + cell.y;
    }

    /// <summary>Runs <paramref name="action"/> at once when the queue is empty (always in Passthrough), else when it drains.</summary>
    public static void WhenDrained(Action action)
    {
        if (action == null)
            return;
        if (!Queueing)
        {
            action();
            return;
        }
        _whenDrained.Add(action);
    }

    /// <summary>
    /// Applies every queued beat at once, in rules order, then runs the pending <see cref="WhenDrained"/> actions. A no-op
    /// in Passthrough, whose queue is always empty.
    /// </summary>
    public static void CatchUpNow()
    {
        // A beat's apply may make a facade call of its own; the queue is taken first, so that call applies at once.
        while (_queue.Count > 0)
        {
            var beats = new List<PresentationBeat>(_queue);
            _queue.Clear();
            for (int i = 0; i < beats.Count; i++)
                ApplyBeat(beats[i]);
        }
        if (_whenDrained.Count > 0)
        {
            var actions = new List<Action>(_whenDrained);
            _whenDrained.Clear();
            for (int i = 0; i < actions.Count; i++)
                actions[i]();
        }
    }

    /// <summary>
    /// Drops the queue and the pending drain actions (the reset sites, step 9). Holding's Clear also snaps every view to
    /// its model and applies the latest cell and area state.
    /// </summary>
    public static void Clear()
    {
        _queue.Clear();
        _whenDrained.Clear();
    }

    /// <summary>Applies one beat: the same apply the facade runs at once in Passthrough.</summary>
    internal static void ApplyBeat(PresentationBeat b)
    {
        if (b == null)
            return;
        switch (b.Kind)
        {
            case BeatKind.Log: if (b.Ui != null) b.Ui.ApplyCombatLog(b.Text); break;
            case BeatKind.TurnSeparator: if (b.Ui != null) b.Ui.ApplyTurnSeparator(b.Index); break;
            case BeatKind.TurnIndicator: if (b.Ui != null) b.Ui.ApplyTurnIndicator(b.Text); break;
            case BeatKind.Initiative: if (b.Ui != null) b.Ui.ApplyInitiativeDisplay(b.Text); break;
            case BeatKind.ActivePC: if (b.Ui != null) b.Ui.ApplyActivePC(b.Index, b.Creature); break;
            case BeatKind.ActiveNPC: if (b.Ui != null) b.Ui.ApplyActiveNPC(b.Index, b.Creature); break;
            case BeatKind.ActionButtonsVisible: if (b.Ui != null) b.Ui.ApplyActionButtonsVisible(b.Flag); break;
            case BeatKind.Panel: CharacterInfoPanel.Apply(b.Panel); break;
            case BeatKind.CellHighlight: if (b.Cell != null) b.Cell.ApplyHighlight(b.Highlight); break;
            case BeatKind.CellOverlay:
                if (b.Cell != null)
                {
                    if (b.Flag) b.Cell.ApplyOverlay(b.Color);
                    else b.Cell.ApplyOverlayClear();
                }
                break;
            case BeatKind.AreaShown:
            case BeatKind.AreaHidden:
            case BeatKind.Control:
            case BeatKind.Interactive:
            case BeatKind.Deferred:
            case BeatKind.QueuedClosure:
                // Only a Defer beat writes the "defer" line (Defer writes it in Passthrough too); a QueuedClosure apply
                // writes its own line, so both paths trace alike.
                if (b.Kind == BeatKind.Deferred)
                    PresentationTrace.Applied("defer", b.Text);
                b.Show?.Invoke();
                break;
        }
    }

    // ── The CombatUI method classes ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The class of every public CombatUI method, by name (overloads share their class). A new public CombatUI method must
    /// be classed here: Tests.UI.CombatPresentationTests.TestCombatUiMethodsClassified reflects over CombatUI and fails
    /// on any unclassed one (docs/DEVELOPMENT_RECIPES.md, "Add a CombatUI method").
    /// </summary>
    public static readonly IReadOnlyDictionary<string, UiCallClass> CombatUiClasses = BuildCombatUiClasses();

    /// <summary>The class of <paramref name="combatUiMethod"/>, or null when it is not classed.</summary>
    public static UiCallClass? ClassOf(string combatUiMethod)
    {
        if (combatUiMethod != null && CombatUiClasses.TryGetValue(combatUiMethod, out UiCallClass c))
            return c;
        return null;
    }

    private static Dictionary<string, UiCallClass> BuildCombatUiClasses()
    {
        var d = new Dictionary<string, UiCallClass>(StringComparer.Ordinal);
        void Add(UiCallClass c, params string[] names)
        {
            foreach (string n in names)
                d[n] = c;
        }

        // Value beats (the log hook ScenarioHooks.CombatLog stays ShowCombatLog's first statement, at rules time).
        Add(UiCallClass.Queued,
            "ShowCombatLog", "AddTurnSeparator", "SetTurnIndicator", "UpdateInitiativeDisplay",
            "SetActivePC", "SetActiveNPC", "SetActionButtonsVisible",
            "UpdateAllStats4PC", "UpdateAllStats", "UpdateAllStatsMultiNPC", "UpdateStats",
            "AddCombatMessage", "LogAttack", "LogDamage");

        Add(UiCallClass.Control, "ShowActionButtonsForCharacter");

        Add(UiCallClass.Interactive, "ShowConfirmationDialog", "ShowDefeatPanel");

        // Prompts, menus and pickers a group never reaches (attacker-side prompts go to the AI decider, by construction).
        Add(UiCallClass.CatchUp,
            "ShowSpecialAttackMenu", "ShowSpecialStyleSelectionMenu", "ShowBullRushExtraPushChoice",
            "ShowAoOConfirmationPrompt", "ShowTouchSpellPrompt", "ShowSpellSelection", "ShowSummonCreatureSelection",
            "ShowSummonContextMenu", "ShowCastDefensivelyPrompt", "ShowPickUpItemSelection", "ShowCharacterSelectionUI",
            "ShowDropEquippedItemSelection", "ShowDisarmWeaponSelection", "ShowSunderItemSelection",
            "ShowDisguiseSelfRaceSelector");

        // Always at once: queries, hides, resets (whose inner Queued calls go through the facade), the PC-turn controls
        // (the action panel is hidden during AI turns and refreshed by the control beat), icons and layout.
        Add(UiCallClass.Live,
            "BuildFlankingIndicator", "SetCurrentTarget", "GetCurrentTarget", "ClearCurrentTarget",
            "ClearCombatLog", "ClearTurnOrderDisplay", "ResetAllUI", "InitializeForCombat", "ShowPositioningInstructions",
            "ExportCombatLog", "RefreshAiSpeedButton", "ResetFloatingWindowLayout",
            "UpdateActionButtons", "RefreshActionButtons", "OnGrappleActionsClicked", "ResetDamageModeToggleVisual",
            "UpdateDamageModeToggle", "UpdateFeatControls", "UpdatePowerAttackLabel", "UpdateRapidShotLabel",
            "SetPCIcon", "SetNPCIcon", "BeginSpontaneousConversion", "ClearSpontaneousCastState",
            "IsSpecialStyleSelectionMenuOpen", "IsDisguiseSelfRaceSelectorOpen",
            "HideSpecialAttackMenu", "HideSpecialStyleSelectionMenu", "HideBullRushExtraPushChoice",
            "HideAoOConfirmationPrompt", "HideSpellSelection", "HideTouchSpellPrompt", "HideSummonContextMenu",
            "HideConfirmationDialog", "HideDefeatPanel", "HideSummonCreatureSelection", "HidePickUpItemSelection",
            "HideDropEquippedItemSelection", "HideDisarmWeaponSelection", "HideDisguiseSelfRaceSelector");
        return d;
    }
}
