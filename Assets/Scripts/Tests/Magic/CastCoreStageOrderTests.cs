#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 7 (docs/designs/cast_core_batch11.md, cast-core-single-target-pc): the staged cast core
/// (GameManager.ExecuteCast, Spell/Resolution/GameManager.CastCore.cs).
/// A recording fake decider (answers every choice inline, lets a threatened cast proceed or cancels it, and records the
/// finish) casts four spells of a wizard 5 with the UI path's legacy quirks: a self buff (Shield), a melee touch spell
/// on an ally (Shocking Grasp), a ray (Ray of Frost) and a buff on another creature (Bull's Strength). Each must run
/// the stages in today's order (with GlobeAfterCast the Globe stage after Resolve, with
/// ConcentrationGateBeforeProvocation ConcentrationOnCast before Provocation), ask the decider for the choices after the
/// target test and for the provocation in the Provocation stage, and finish once, Resolved at CombatEnd, with the cast
/// ended. A Self spell aimed at another creature is refused at Legality with nothing spent; a decider that refuses a
/// choice ends the cast at Legality with nothing spent; a decider that suspends at a choice leaves the cast open with no
/// outcome, and the next run starts again at Legality and finishes once; a cancel at the provocation restores the
/// action and the slot; a run waiting for the provocation answer refuses a re-entry. No stage touches the UI: a fake-decider cast leaves CurrentSubPhase and the last
/// combat log as they were, and the source of GameManager.CastCore.cs uses CombatUI only to log
/// (CombatUI?.ShowCombatLog) and never calls ShowActionChoices, AfterAttackDelay, UpdateAllStatsUI or the highlights,
/// nor reads IsControllable. The builders set LegacyCastQuirkSets.UiPath (an explicit list of today's PC-path bits),
/// and a request lacking a bit a stage still needs raises a cast-core violation. Play mode (needs the scene GameManager); the source scan also runs in edit mode.
/// </summary>
public static class CastCoreStageOrderTests
{
    private static int _passed;
    private static int _failed;

    /// <summary>The stages of a fully resolved single-target cast with the UI path's quirks, with the decider's calls.</summary>
    private static readonly string[] ResolvedSequence =
    {
        "Legality", "decider:fill", "Cost", "Distraction", "Failure", "Spend", "ConcentrationOnCast", "Provocation",
        "decider:provocation", "Counterspell", "RingCounterspell", "InvisibilityBreak", "MirrorImage", "TargetBlink",
        "Resolve", "Globe", "Handlers", "Buff", "PostCast", "CombatEnd", "decider:before-combat-end",
        "decider:finished:Resolved@CombatEnd"
    };

    /// <summary>
    /// Today's PC single-target path, bit by bit (batch 11 step 7). Step 8 adds the AI path's own set; NPC-only bits must
    /// never join this list (PC and NPC follow identical rules; only the decider differs).
    /// </summary>
    private static readonly LegacyCastQuirk[] PcPathBits =
    {
        LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath,
        LegacyCastQuirk.GlobeAfterCast,
        LegacyCastQuirk.EntangledUsesCasterLevelFormula,
        LegacyCastQuirk.QuickenedProvokes,
        LegacyCastQuirk.SlotFailureClearsSnapshot,
        LegacyCastQuirk.ConcentrationGateBeforeProvocation,
        LegacyCastQuirk.RangeHighlightUsesCharacterLevel,
        LegacyCastQuirk.SoundBurstStunIgnoresSuccess,
        LegacyCastQuirk.AnimateRopeIgnoresSuccess,
        LegacyCastQuirk.AlliesForgoEverySave,
        LegacyCastQuirk.DeafenedRolledInSpellCaster,
        LegacyCastQuirk.ScrollSkipsArcaneFailure,
        LegacyCastQuirk.HeldChargeDeliveryIsFree,
        LegacyCastQuirk.QuickenBeforeSpontaneousFullRound,
        LegacyCastQuirk.SpontaneousFullRoundByCasterClass,
    };

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== CAST CORE STAGE ORDER TESTS (batch 11 step 7) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        TestQuirkSets();
        TestCoreSourceTouchesNoUi();

        if (GameManager.Instance != null)
            TestCasts(GameManager.Instance);
        else
            Debug.Log("  SKIP: the cast runs (no scene GameManager; they run in the Play pass)");

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    /// <summary>
    /// The recording fake decider: answers the choices with <see cref="Fills"/> (one per run, then
    /// <see cref="CastChoiceFill.Filled"/>) and the provocation with <see cref="Answer"/>, at once or, with
    /// <see cref="HoldProvocation"/>, only when the test calls <see cref="PendingAnswer"/>.
    /// </summary>
    private sealed class RecordingDecider : ICastDecider
    {
        public readonly List<string> Events;
        public readonly Queue<CastChoiceFill> Fills = new Queue<CastChoiceFill>();
        public CastProvocationAnswer Answer = CastProvocationAnswer.Proceeds;
        public bool HoldProvocation;
        public Action<CastProvocationAnswer> PendingAnswer;
        public int Finishes;

        public RecordingDecider(List<string> events) { Events = events; }

        public bool IsSynchronous => true;

        public CastChoiceFill FillChoices(GameManager gm, CastContext ctx)
        {
            Events.Add("decider:fill");
            return Fills.Count > 0 ? Fills.Dequeue() : CastChoiceFill.Filled;
        }

        public void ChooseProvocation(GameManager gm, CastContext ctx, Action<CastProvocationAnswer> answer)
        {
            Events.Add("decider:provocation");
            if (HoldProvocation)
                PendingAnswer = answer;
            else
                answer(Answer);
        }

        public void BeforeCombatEndCheck(GameManager gm, CastContext ctx)
        {
            Events.Add("decider:before-combat-end");
        }

        public void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome)
        {
            Finishes++;
            Events.Add("decider:finished:" + outcome.Kind + "@" + outcome.At);
        }
    }

    private static void TestQuirkSets()
    {
        SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        SpellCastRequest ui = SpellCastRequest.ForUi(null, missile, null, null);
        Assert(ui.Quirks == LegacyCastQuirkSets.UiPath, "ForUi sets the UI path's legacy quirks", "" + ui.Quirks);
        SpellCastRequest prebuilt = SpellCastRequest.ForUiPrebuilt(null, missile.Clone(), null, new CastSource { Kind = CastSourceKind.Scroll });
        Assert(prebuilt.Quirks == LegacyCastQuirkSets.UiPath, "ForUiPrebuilt sets the UI path's legacy quirks", "" + prebuilt.Quirks);
        Assert(ui.HasQuirk(LegacyCastQuirk.GlobeAfterCast | LegacyCastQuirk.QuickenedProvokes) && !ui.HasQuirk((LegacyCastQuirk)(1 << 30)),
            "HasQuirk tests every bit it is given");

        LegacyCastQuirk listed = LegacyCastQuirk.None;
        foreach (LegacyCastQuirk bit in PcPathBits)
        {
            Assert(Enum.IsDefined(typeof(LegacyCastQuirk), bit) && bit != LegacyCastQuirk.None, "The PC-path bit " + bit + " is one named flag");
            listed |= bit;
        }
        Assert(LegacyCastQuirkSets.UiPath == listed, "The UI path's quirk set is exactly today's PC-path bits (" + PcPathBits.Length + ")",
            "UiPath " + LegacyCastQuirkSets.UiPath + " vs listed " + listed);
    }

    /// <summary>The cast core's source: CombatUI only to log, and none of the UI follow-up.</summary>
    private static void TestCoreSourceTouchesNoUi()
    {
        string path = Path.Combine(Application.dataPath, "Scripts/Spell/Resolution/GameManager.CastCore.cs");
        if (!File.Exists(path))
        {
            Assert(false, "GameManager.CastCore.cs exists for the source scan", path);
            return;
        }

        var code = new System.Text.StringBuilder();
        foreach (string line in File.ReadAllLines(path))
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            code.AppendLine(comment >= 0 ? line.Substring(0, comment) : line);
        }
        string src = code.ToString();

        int uses = 0, logs = 0;
        foreach (Match m in Regex.Matches(src, @"CombatUI\b(.{0,20})"))
        {
            uses++;
            if (m.Groups[1].Value.StartsWith("?.ShowCombatLog(", StringComparison.Ordinal))
                logs++;
        }
        Assert(uses > 0 && uses == logs, "Every CombatUI use in the cast core's stages is CombatUI?.ShowCombatLog", logs + " of " + uses);

        foreach (string forbidden in new[] { "ShowActionChoices", "AfterAttackDelay", "UpdateAllStatsUI", "ClearAllHighlights", "CurrentSubPhase", "_lastCombatLog", "IsControllable", "_pendingSpell", "StartCoroutine" })
            Assert(!src.Contains(forbidden), "The cast core's code does not use " + forbidden);
    }

    private static void TestCasts(GameManager gm)
    {
        if (gm.Harness_OpenCastCount != 0)
        {
            Debug.Log("  SKIP: the cast runs (a cast is open in this Play session: " + gm.Harness_OpenCastCount + ")");
            return;
        }

        var violations = new List<string>();
        Action<string> oldHook = ScenarioHooks.CastStackViolation;
        ScenarioHooks.CastStackViolation = d => violations.Add(d);

        CharacterController caster = null;
        CharacterController ally = null;
        try
        {
            caster = TestHelpers.CreateCharacter("CastCoreWizard", "Wizard", level: 5, con: 14, intelligence: 18, bab: 2);
            ally = TestHelpers.CreateCharacter("CastCoreAlly", "Fighter", level: 20, con: 30, bab: 20, gridPosition: new Vector2Int(1, 0));
            caster.SetTeam(CharacterTeam.Player);
            ally.SetTeam(CharacterTeam.Player);
            SpellcastingComponent sc = caster.gameObject.AddComponent<SpellcastingComponent>();
            sc.Init(caster.Stats);
            sc.SpellSlots.Clear();

            GameManager.PlayerSubPhase subPhaseBefore = gm.CurrentSubPhase;
            string lastLogBefore = gm.Harness_LastCombatLog;

            RunResolved(gm, sc, caster, caster, SpellNames.SHIELD, "a self buff (Shield)", violations);
            RunResolved(gm, sc, caster, ally, SpellNames.SHOCKING_GRASP, "a touch spell on an ally (Shocking Grasp)", violations);
            RunResolved(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, "a ray (Ray of Frost)", violations);
            RunResolved(gm, sc, caster, ally, SpellNames.BULLS_STRENGTH, "a buff on another creature (Bull's Strength)", violations);

            Assert(gm.CurrentSubPhase == subPhaseBefore, "No stage set CurrentSubPhase (the UI decider's finish does)", subPhaseBefore + " -> " + gm.CurrentSubPhase);
            Assert(gm.Harness_LastCombatLog == lastLogBefore, "No stage wrote the last combat log (the UI decider's finish does)");
            Assert(ally.Stats.CurrentHP > 0, "The ally survived the test casts (no combat-end check was due)", "" + ally.Stats.CurrentHP);

            TestRefusal(gm, sc, caster, ally);
            TestChoiceRefused(gm, sc, caster, ally);
            TestChoiceSuspended(gm, sc, caster, ally);
            TestCancel(gm, sc, caster, ally);

            Assert(violations.Count == 0, "No cast-core or cast-stack violation for UI-path requests", string.Join("; ", violations));
            TestReentryWhileAwaitingProvocation(gm, sc, caster, ally, violations);
            TestMissingQuirk(gm, sc, caster, violations);
        }
        finally
        {
            ScenarioHooks.CastStackViolation = oldHook;
            if (gm.Harness_OpenCastCount > 0)
                gm.Harness_EndAllCasts("CastCoreStageOrderTests cleanup");
            if (caster != null)
                Object.DestroyImmediate(caster.gameObject);
            if (ally != null)
                Object.DestroyImmediate(ally.gameObject);
        }
    }

    /// <summary>A UI-path request for <paramref name="spellId"/> with a fresh slot, cast by the recording decider.</summary>
    private static CastContext Begin(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController target,
        string spellId, RecordingDecider decider, out SpellSlot slot)
    {
        SpellData template = SpellDatabase.GetSpell(spellId);
        slot = new SpellSlot(template.SpellLevel, template.Clone(), false, "Wizard");
        sc.SpellSlots.Add(slot);
        caster.Actions.Reset();

        SpellCastRequest req = SpellCastRequest.ForUi(caster, template, null, new CastSource());
        req.Spell.HasMaterialComponent = false; // the test wizard carries no component pouch (the request's own clone)
        req.Aim.Target = target;
        req.Decider = decider;
        CastContext ctx = gm.BeginCast(req);
        ctx.StageObserver = stage => decider.Events.Add(stage.ToString());
        return ctx;
    }

    private static void RunResolved(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController target,
        string spellId, string label, List<string> violations)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        CastContext ctx = Begin(gm, sc, caster, target, spellId, decider, out SpellSlot slot);

        CastOutcome done = null;
        int doneCalls = 0;
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });

        Assert(string.Join(",", events) == string.Join(",", ResolvedSequence),
            label + ": the stages run in today's order, with the decider asked for the choices and the provocation",
            string.Join(",", events));
        Assert(done != null && doneCalls == 1 && decider.Finishes == 1 && ReferenceEquals(done, ctx.Outcome)
               && done.Kind == CastOutcomeKind.Resolved && done.At == CastStage.CombatEnd && !done.CombatEnded,
            label + ": FinishCast runs once and gives Resolved at CombatEnd to the decider and the run", "" + done);
        Assert(ctx.Ended && gm.Harness_OpenCastCount == 0, label + ": the cast is ended (EndCast) when it finishes");
        Assert(slot.IsUsed && caster.Actions.StandardActionUsed, label + ": the slot and the standard action are spent");
        Assert(ctx.Result != null && !string.IsNullOrEmpty(ctx.ResultLog) && ctx.ResultLog.StartsWith(ctx.LogPrefix, StringComparison.Ordinal),
            label + ": the result log is built once, with the cast's (empty) log prefix");
    }

    private static void TestRefusal(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        CastContext ctx = Begin(gm, sc, caster, ally, SpellNames.SHIELD, decider, out SpellSlot slot);
        gm.ExecuteCast(ctx, null);

        Assert(string.Join(",", events) == "Legality,decider:finished:Refused@Legality",
            "A Self spell aimed at another creature is refused at Legality, before the decider is asked", string.Join(",", events));
        Assert(ctx.Outcome != null && ctx.Outcome.Reason == "invalid-target" && ctx.Outcome.BackToActionMenu, "The refusal's reason is invalid-target and it goes back to the action menu");
        Assert(ctx.Ended && !slot.IsUsed && !caster.Actions.StandardActionUsed, "A refused cast is ended and spends neither the slot nor the action");

        SpellCastRequest probe = SpellCastRequest.ForUi(caster, SpellDatabase.GetSpell(SpellNames.SHIELD), null, new CastSource());
        probe.Aim.Target = ally;
        int openBefore = gm.Harness_OpenCastCount;
        CastOutcome legality = gm.CheckLegality(probe);
        probe.Aim.Target = caster;
        CastOutcome legal = gm.CheckLegality(probe);
        Assert(legality.Kind == CastOutcomeKind.Refused && legality.Reason == "invalid-target" && legal.Kind == CastOutcomeKind.Resolved
               && gm.Harness_OpenCastCount == openBefore && !caster.Actions.StandardActionUsed,
            "CheckLegality refuses the wrong target and passes the right one, with no side effect", legality + " / " + legal);
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>A decider that refuses a choice (a weapon picker with no weapon) ends the cast at Legality with nothing spent.</summary>
    private static void TestChoiceRefused(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        decider.Fills.Enqueue(CastChoiceFill.Refused);
        CastContext ctx = Begin(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, decider, out SpellSlot slot);
        CastOutcome done = null;
        int doneCalls = 0;
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });

        Assert(string.Join(",", events) == "Legality,decider:fill,decider:finished:Refused@Legality",
            "A refused choice ends the cast at Legality", string.Join(",", events));
        Assert(done != null && doneCalls == 1 && done.Reason == "choice-refused" && done.BackToActionMenu,
            "The refusal's reason is choice-refused and it goes back to the action menu", "" + done);
        Assert(ctx.Ended && gm.Harness_OpenCastCount == 0 && !slot.IsUsed && !caster.Actions.StandardActionUsed,
            "A cast refused at the choice is ended and spends neither the slot nor the action");
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// A decider that suspends at a choice (a dialog opened) leaves the cast open with no outcome and nothing spent; the
    /// re-entry with the answer runs again from Legality and finishes once.
    /// </summary>
    private static void TestChoiceSuspended(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        decider.Fills.Enqueue(CastChoiceFill.Suspended);
        CastContext ctx = Begin(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, decider, out SpellSlot slot);
        CastOutcome done = null;
        int doneCalls = 0;
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });

        Assert(string.Join(",", events) == "Legality,decider:fill", "A suspended choice stops the run in Legality", string.Join(",", events));
        Assert(ctx.Outcome == null && done == null && decider.Finishes == 0 && !ctx.Ended && gm.Harness_OpenCastCount == 1,
            "A suspended run has no outcome, calls neither continuation and leaves the cast open", "open casts " + gm.Harness_OpenCastCount);
        Assert(!slot.IsUsed && !caster.Actions.StandardActionUsed, "A suspended run spends neither the slot nor the action");

        events.Clear();
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });
        Assert(string.Join(",", events) == string.Join(",", ResolvedSequence),
            "The re-entry after the dialog runs every stage again from Legality", string.Join(",", events));
        Assert(done != null && doneCalls == 1 && decider.Finishes == 1 && done.Kind == CastOutcomeKind.Resolved
               && ctx.Ended && gm.Harness_OpenCastCount == 0 && slot.IsUsed && caster.Actions.StandardActionUsed,
            "The resumed cast finishes once, ended, with the slot and the action spent", "" + done);
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// A run waiting for the provocation answer (the PC's AoO prompt) refuses a second run of the same cast: one
    /// violation, no reset, nothing spent twice; the answer then finishes the first run once.
    /// </summary>
    private static void TestReentryWhileAwaitingProvocation(GameManager gm, SpellcastingComponent sc, CharacterController caster,
        CharacterController ally, List<string> violations)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events) { HoldProvocation = true };
        CastContext ctx = Begin(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, decider, out SpellSlot slot);
        int doneCalls = 0;
        gm.ExecuteCast(ctx, o => doneCalls++);
        int slotLevel = ctx.SlotLevelToConsume;
        bool compSet = ctx.SpellComp != null;

        int before = violations.Count;
        int reentryDoneCalls = 0;
        gm.ExecuteCast(ctx, o => reentryDoneCalls++);
        Assert(violations.Count == before + 1 && violations[violations.Count - 1].Contains("re-entered"),
            "A re-entry while the run waits for the provocation answer raises one violation", string.Join("; ", violations));
        Assert(compSet && ctx.SpellComp != null && ctx.SlotLevelToConsume == slotLevel && ctx.Outcome == null && reentryDoneCalls == 0
               && events.FindAll(e => e == "Legality").Count == 1,
            "...and neither resets the waiting run nor starts another", string.Join(",", events));

        if (decider.PendingAnswer != null)
            decider.PendingAnswer(CastProvocationAnswer.Proceeds);
        Assert(doneCalls == 1 && decider.Finishes == 1 && ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && ctx.Ended && slot.IsUsed,
            "The answer then finishes the first run once", "" + ctx.Outcome);
        violations.RemoveRange(before, violations.Count - before);
        sc.SpellSlots.Remove(slot);
    }

    private static void TestCancel(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events) { Answer = CastProvocationAnswer.Cancelled };
        CastContext ctx = Begin(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, decider, out SpellSlot slot);
        gm.ExecuteCast(ctx, null);

        Assert(string.Join(",", events) == "Legality,decider:fill,Cost,Distraction,Failure,Spend,ConcentrationOnCast,Provocation,decider:provocation,decider:finished:Cancelled@Provocation",
            "A cast cancelled at the provocation stops there", string.Join(",", events));
        Assert(ctx.Ended && !slot.IsUsed && !caster.Actions.StandardActionUsed, "The cancel gives back the slot and the action (the snapshot)",
            "slot used " + slot.IsUsed + ", standard used " + caster.Actions.StandardActionUsed);
        sc.SpellSlots.Remove(slot);
    }

    private static void TestMissingQuirk(GameManager gm, SpellcastingComponent sc, CharacterController caster, List<string> violations)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        CastContext ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, decider, out SpellSlot slot);
        ctx.Request.Quirks = LegacyCastQuirkSets.UiPath & ~LegacyCastQuirk.GlobeAfterCast;
        int before = violations.Count;
        gm.ExecuteCast(ctx, null);

        Assert(violations.Count == before + 1 && violations[violations.Count - 1].Contains("GlobeAfterCast"),
            "A request without a quirk a stage still needs raises one cast-core violation naming it", string.Join("; ", violations));
        Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && events.IndexOf("Resolve") < events.IndexOf("Globe"),
            "...and gets the legacy behaviour anyway (the Globe stage after Resolve)", string.Join(",", events));
        sc.SpellSlots.Remove(slot);
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }
}
}
#endif
