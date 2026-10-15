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
///
/// Batch 11 step 8 (cast-core-npc-single-target): the NPC path (SpellCastRequest.ForAI, GameManager.RunCastSync, the
/// AI decider) runs the same four casts through the same stages, except where an NPC quirk bit moves or skips one
/// (listed in <see cref="DeriveAiSequence"/>); ForAI sets LegacyCastQuirkSets.AiPath, which differs from UiPath only by
/// the listed parity bits; RunCastSync returns the old NPC executor's bool (true once the slot is spent, false for a
/// refusal) and refuses a decider that is not synchronous, and a run that suspends anyway returns false (Refused) with one
/// violation; an AI cast leaves an outer cast's cancel snapshot in place; the NPC Blink branches (the slot spent before
/// the failure checks, the NPC dice labels, no log for a passed check, a miss on a blinking target) hold with forced
/// dice; the AI's refusals log nothing; and the core's source names no decider type and never asks who controls the caster.
///
/// Batch 11 step 9 (cast-core-area): SpellCastRequest.ForUiArea sets CastResolveKind.Area, no single target and
/// LegacyCastQuirkSets.UiAreaPath (an explicit list: the UI path's bits an area cast shows plus the Area* bits, which no
/// other set carries). Area casts by the recording decider (a plain Burning Hands through the generic loop, a quickened
/// one, and a quickened Sleep, a material-component spell the pouchless wizard may cast because the area path skips the
/// pouch) run the shared stages without the Ring of Counterspells, then the area Resolve and CombatEnd stages, and finish
/// once, Resolved at CombatEnd, ended, with the slot and action spent; the generic loop's log carries the cast's
/// quickened prefix and Sleep's does not (SPL-161). Skipped when a side is already out (the area exit runs the
/// combat-end check).
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

    /// <summary>The stages of a resolved area cast with the area path's quirks (batch 11 step 9), with the decider's calls.</summary>
    private static readonly string[] AreaResolvedSequence =
    {
        "Legality", "decider:fill", "Cost", "Distraction", "Failure", "Spend", "ConcentrationOnCast", "Provocation",
        "decider:provocation", "Counterspell", "InvisibilityBreak", "Resolve", "CombatEnd", "decider:before-combat-end",
        "decider:finished:Resolved@CombatEnd"
    };

    /// <summary>Today's PC area path, bit by bit (batch 11 step 9).</summary>
    private static readonly LegacyCastQuirk[] AreaPathBits =
    {
        LegacyCastQuirk.QuickenedProvokes,
        LegacyCastQuirk.SlotFailureClearsSnapshot,
        LegacyCastQuirk.ConcentrationGateBeforeProvocation,
        LegacyCastQuirk.AlliesForgoEverySave,
        LegacyCastQuirk.DeafenedRolledInSpellCaster,
        LegacyCastQuirk.ScrollSkipsArcaneFailure,
        LegacyCastQuirk.QuickenBeforeSpontaneousFullRound,
        LegacyCastQuirk.SpontaneousFullRoundByCasterClass,
        LegacyCastQuirk.AreaSkipsEntangled,                 // SPL-006
        LegacyCastQuirk.AreaSkipsBlink,                     // SPL-006
        LegacyCastQuirk.AreaSkipsRing,                      // SPL-006
        LegacyCastQuirk.AreaSkipsPouch,                     // SPL-006, SPL-114
        LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers,    // CORE-039
        LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers,    // SPL-161
    };

    /// <summary>
    /// The bits by which the AI path's quirk set differs from the UI path's (batch 11 step 8), each naming a PC/NPC parity
    /// issue: the two PC-only bits it drops and the NPC bits it adds.
    /// </summary>
    private static readonly LegacyCastQuirk[] AiPathDifferences =
    {
        LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath,      // dropped: CORE-039 (the NPC wrapper checks after every cast)
        LegacyCastQuirk.RangeHighlightUsesCharacterLevel,        // dropped: SPL-017 (the PC targeting highlight)
        LegacyCastQuirk.NpcOwnLegalityChecks,                    // SPL-160
        LegacyCastQuirk.NpcRangeUsesCasterLevel,                 // SPL-017
        LegacyCastQuirk.NpcRefusesArea,                          // AI-001
        LegacyCastQuirk.NpcNullMetamagic,                        // AI-001, SPL-038
        LegacyCastQuirk.NpcNoComponentCheck,                     // SPL-114
        LegacyCastQuirk.NpcNoConcentrationOnCastCheck,           // SPL-114
        LegacyCastQuirk.NpcNoConcentrationTracking,              // SPL-114
        LegacyCastQuirk.NpcInvisibilityBreakBeforeCounterspell,  // SPL-118
        LegacyCastQuirk.NpcSpendsBeforeFailureChecks,            // SPL-160
        LegacyCastQuirk.NpcBlinkLabelsAndSilentPass,             // SPL-160
        LegacyCastQuirk.NpcResultLogBeforeTargetDeath,           // SPL-160
        LegacyCastQuirk.NpcCombatEndCheckedByCaller,             // CORE-039
        LegacyCastQuirk.NpcNoMirrorImageSwap,                    // SPL-117
    };

    /// <summary>
    /// The AI path's stage sequence from the PC path's, by the NPC quirk bits that move or skip a stage:
    /// NpcSpendsBeforeFailureChecks (Spend before Failure), NpcNoConcentrationOnCastCheck (no ConcentrationOnCast) and
    /// NpcInvisibilityBreakBeforeCounterspell (InvisibilityBreak before Counterspell). Every other stage is the same.
    /// </summary>
    private static List<string> DeriveAiSequence(IEnumerable<string> pcSequence)
    {
        var seq = new List<string>(pcSequence);
        seq.Remove("ConcentrationOnCast");
        MoveBefore(seq, "Spend", "Failure");
        MoveBefore(seq, "InvisibilityBreak", "Counterspell");
        return seq;
    }

    private static void MoveBefore(List<string> seq, string item, string before)
    {
        if (!seq.Contains(item) || !seq.Contains(before))
            return;
        seq.Remove(item);
        seq.Insert(seq.IndexOf(before), item);
    }

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
        Assert(ui.HasQuirk(LegacyCastQuirk.GlobeAfterCast | LegacyCastQuirk.QuickenedProvokes) && !ui.HasQuirk((LegacyCastQuirk)(1L << 62)),
            "HasQuirk tests every bit it is given");

        LegacyCastQuirk listed = LegacyCastQuirk.None;
        foreach (LegacyCastQuirk bit in PcPathBits)
        {
            Assert(Enum.IsDefined(typeof(LegacyCastQuirk), bit) && bit != LegacyCastQuirk.None, "The PC-path bit " + bit + " is one named flag");
            listed |= bit;
        }
        Assert(LegacyCastQuirkSets.UiPath == listed, "The UI path's quirk set is exactly today's PC-path bits (" + PcPathBits.Length + ")",
            "UiPath " + LegacyCastQuirkSets.UiPath + " vs listed " + listed);

        // Batch 11 step 8: the AI path's set.
        SpellCastRequest ai = SpellCastRequest.ForAI(null, null, missile);
        Assert(ai.Quirks == LegacyCastQuirkSets.AiPath && ai.Decider is AiCastDecider, "ForAI sets the AI path's legacy quirks and the AI decider", "" + ai.Quirks);
        LegacyCastQuirk differences = LegacyCastQuirk.None;
        foreach (LegacyCastQuirk bit in AiPathDifferences)
        {
            Assert(Enum.IsDefined(typeof(LegacyCastQuirk), bit) && bit != LegacyCastQuirk.None, "The AI-path difference " + bit + " is one named flag");
            differences |= bit;
        }
        Assert((LegacyCastQuirkSets.AiPath ^ LegacyCastQuirkSets.UiPath) == differences,
            "The AI path's quirk set differs from the UI path's exactly by the listed parity bits (" + AiPathDifferences.Length + ")",
            "AiPath ^ UiPath = " + (LegacyCastQuirkSets.AiPath ^ LegacyCastQuirkSets.UiPath));
        foreach (string name in Enum.GetNames(typeof(LegacyCastQuirk)))
        {
            if (!name.StartsWith("Npc", StringComparison.Ordinal))
                continue;
            var bit = (LegacyCastQuirk)Enum.Parse(typeof(LegacyCastQuirk), name);
            Assert((LegacyCastQuirkSets.UiPath & bit) == 0 && (LegacyCastQuirkSets.AiPath & bit) == bit
                   && (LegacyCastQuirkSets.UiAreaPath & bit) == 0,
                "The NPC bit " + name + " is set by the AI builder only");
        }

        // Batch 11 step 9: the area path's set.
        SpellCastRequest area = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(null, missile, null, null),
            new List<CharacterController>(), new HashSet<Vector2Int>());
        Assert(area.Quirks == LegacyCastQuirkSets.UiAreaPath && area.ResolveKind == CastResolveKind.Area && area.Aim.Target == null
               && ui.ResolveKind == CastResolveKind.Single && ai.ResolveKind == CastResolveKind.Single,
            "ForUiArea makes an area request with no single target and the area path's legacy quirks (the other builders stay Single)", "" + area.Quirks);
        LegacyCastQuirk listedArea = LegacyCastQuirk.None;
        foreach (LegacyCastQuirk bit in AreaPathBits)
        {
            Assert(Enum.IsDefined(typeof(LegacyCastQuirk), bit) && bit != LegacyCastQuirk.None, "The area-path bit " + bit + " is one named flag");
            listedArea |= bit;
        }
        Assert(LegacyCastQuirkSets.UiAreaPath == listedArea, "The area path's quirk set is exactly today's area-path bits (" + AreaPathBits.Length + ")",
            "UiAreaPath " + LegacyCastQuirkSets.UiAreaPath + " vs listed " + listedArea);
        foreach (string name in Enum.GetNames(typeof(LegacyCastQuirk)))
        {
            if (!name.StartsWith("Area", StringComparison.Ordinal))
                continue;
            var bit = (LegacyCastQuirk)Enum.Parse(typeof(LegacyCastQuirk), name);
            Assert((LegacyCastQuirkSets.UiAreaPath & bit) == bit && (LegacyCastQuirkSets.UiPath & bit) == 0 && (LegacyCastQuirkSets.AiPath & bit) == 0,
                "The area bit " + name + " is set by the area builder only");
        }
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

        // Batch 11 step 8: no stage asks who controls the caster or which decider it has; only the entry points choose
        // the decider (the request's quirk bits carry the NPC path's differences).
        foreach (string forbidden in new[] { "IsPlayerControlled", "IsPC", "ActivePC", "AiCastDecider", "UiCastDecider", "is ICastDecider" })
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

            // Batch 11 step 8: the AI path through RunCastSync.
            RunAiResolved(gm, sc, caster, caster, SpellNames.SHIELD, "AI: a self buff (Shield)");
            RunAiResolved(gm, sc, caster, ally, SpellNames.SHOCKING_GRASP, "AI: a touch spell on an ally (Shocking Grasp)");
            RunAiResolved(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, "AI: a ray (Ray of Frost)");
            RunAiResolved(gm, sc, caster, ally, SpellNames.BULLS_STRENGTH, "AI: a buff on another creature (Bull's Strength)");
            Assert(gm.CurrentSubPhase == subPhaseBefore, "No AI cast set CurrentSubPhase", subPhaseBefore + " -> " + gm.CurrentSubPhase);
            TestAiRefusals(gm, sc, caster, ally);
            Assert(violations.Count == 0, "No cast-core or cast-stack violation for AI-path requests", string.Join("; ", violations));
            TestRunCastSyncRefusesUiDecider(gm, sc, caster, violations);
            TestRunCastSyncSuspendedRun(gm, sc, caster, violations);
            TestAiCastKeepsOuterSnapshot(gm, sc, caster, ally);
            TestAiBlink(gm, sc, caster, ally);
            Assert(violations.Count == 0, "No cast-core or cast-stack violation for the AI-path failure cases", string.Join("; ", violations));

            TestReentryWhileAwaitingProvocation(gm, sc, caster, ally, violations);
            TestMissingQuirk(gm, sc, caster, violations);

            // Batch 11 step 9: area casts through the same core.
            int violationsBeforeArea = violations.Count;
            TestAreaCasts(gm, sc, caster);
            Assert(violations.Count == violationsBeforeArea, "No cast-core or cast-stack violation for area requests",
                string.Join("; ", violations.GetRange(violationsBeforeArea, violations.Count - violationsBeforeArea)));
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

    /// <summary>
    /// Records the AI decider's calls and passes them on to <see cref="AiCastDecider"/> (the real NPC answers: the
    /// defaults, the threatened-cast decision and the follow-up).
    /// </summary>
    private sealed class RecordingAiDecider : ICastDecider
    {
        public readonly List<string> Events;
        public RecordingAiDecider(List<string> events) { Events = events; }
        public bool IsSynchronous => AiCastDecider.Instance.IsSynchronous;

        public CastChoiceFill FillChoices(GameManager gm, CastContext ctx)
        {
            Events.Add("decider:fill");
            return AiCastDecider.Instance.FillChoices(gm, ctx);
        }

        public void ChooseProvocation(GameManager gm, CastContext ctx, Action<CastProvocationAnswer> answer)
        {
            Events.Add("decider:provocation");
            AiCastDecider.Instance.ChooseProvocation(gm, ctx, answer);
        }

        public void BeforeCombatEndCheck(GameManager gm, CastContext ctx)
        {
            Events.Add("decider:before-combat-end");
            AiCastDecider.Instance.BeforeCombatEndCheck(gm, ctx);
        }

        public void OnCastFinished(GameManager gm, CastContext ctx, CastOutcome outcome)
        {
            Events.Add("decider:finished:" + outcome.Kind + "@" + outcome.At);
            AiCastDecider.Instance.OnCastFinished(gm, ctx, outcome);
        }
    }

    /// <summary>An AI-path request (ForAI) for <paramref name="spellId"/> with a fresh slot and the caster's actions reset.</summary>
    private static SpellCastRequest BeginAi(SpellcastingComponent sc, CharacterController caster, CharacterController target,
        string spellId, List<string> events, out SpellSlot slot)
    {
        SpellData template = SpellDatabase.GetSpell(spellId);
        slot = new SpellSlot(template.SpellLevel, template.Clone(), false, "Wizard");
        sc.SpellSlots.Add(slot);
        caster.Actions.Reset();

        SpellCastRequest req = SpellCastRequest.ForAI(caster, target, template);
        if (events != null)
            req.Decider = new RecordingAiDecider(events);
        return req;
    }

    private static void RunAiResolved(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController target,
        string spellId, string label)
    {
        var events = new List<string>();
        SpellCastRequest req = BeginAi(sc, caster, target, spellId, events, out SpellSlot slot);
        bool cast = gm.RunCastSync(req, out CastOutcome outcome, stage => events.Add(stage.ToString()));

        List<string> expected = DeriveAiSequence(ResolvedSequence);
        Assert(string.Join(",", events) == string.Join(",", expected),
            label + ": the AI path runs the PC path's stages, moved or skipped only by the NPC quirk bits (Spend before Failure, no ConcentrationOnCast, InvisibilityBreak before Counterspell)",
            string.Join(",", events));
        Assert(cast && outcome != null && outcome.Kind == CastOutcomeKind.Resolved && outcome.At == CastStage.CombatEnd && outcome.WasCast && !outcome.CombatEnded,
            label + ": RunCastSync returns true with Resolved at CombatEnd (no combat-end check in the stage: the NPC wrapper runs it)", "" + outcome);
        Assert(gm.Harness_OpenCastCount == 0, label + ": the AI cast is ended when RunCastSync returns");
        Assert(slot.IsUsed && caster.Actions.StandardActionUsed, label + ": the slot and the standard action are spent");
        Assert(gm.Harness_LastCombatLog != null && gm.Harness_LastCombatLog.Contains(SpellDatabase.GetSpell(spellId).Name),
            label + ": the AI follow-up keeps the result log as the last combat log, as the old NPC executor did");
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// The AI path's refusals: RunCastSync returns false (the old NPC executor's false) with nothing spent and no
    /// combat-log line, for a caster with no standard action, a Self spell aimed at another creature, a target out of
    /// range and an area spell.
    /// </summary>
    private static void TestAiRefusals(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var logs = new List<string>();
        Action<string> oldLog = ScenarioHooks.CombatLog;
        ScenarioHooks.CombatLog = m => logs.Add(m);
        try
        {
            var events = new List<string>();
            SpellCastRequest noAction = BeginAi(sc, caster, caster, SpellNames.SHIELD, events, out SpellSlot slot);
            caster.Actions.UseStandardAction();
            bool cast = gm.RunCastSync(noAction, out CastOutcome o, s => events.Add(s.ToString()));
            Assert(!cast && o.Kind == CastOutcomeKind.Refused && o.Reason == "no-standard-action" && !slot.IsUsed
                   && string.Join(",", events) == "Legality,decider:finished:Refused@Legality",
                "AI: a caster with no standard action is refused at Legality (false, nothing spent)", o + " " + string.Join(",", events));
            sc.SpellSlots.Remove(slot);

            SpellCastRequest wrongTarget = BeginAi(sc, caster, ally, SpellNames.SHIELD, null, out slot);
            cast = gm.RunCastSync(wrongTarget, out o);
            Assert(!cast && o.Reason == "invalid-target" && !slot.IsUsed && !caster.Actions.StandardActionUsed,
                "AI: a Self spell aimed at another creature is refused (false, nothing spent)", "" + o);
            sc.SpellSlots.Remove(slot);

            CharacterController far = TestHelpers.CreateCharacter("CastCoreFar", "Fighter", level: 1, gridPosition: new Vector2Int(40, 0));
            try
            {
                far.SetTeam(CharacterTeam.Player);
                SpellCastRequest outOfRange = BeginAi(sc, caster, far, SpellNames.SHOCKING_GRASP, null, out slot);
                cast = gm.RunCastSync(outOfRange, out o);
                Assert(!cast && o.Reason == "out-of-range" && !slot.IsUsed && !caster.Actions.StandardActionUsed,
                    "AI: a touch spell at a creature 40 squares away is refused by the caster-level range (NpcRangeUsesCasterLevel)", "" + o);
                sc.SpellSlots.Remove(slot);
            }
            finally
            {
                Object.DestroyImmediate(far.gameObject);
            }

            SpellCastRequest area = BeginAi(sc, caster, ally, SpellNames.BURNING_HANDS, null, out slot);
            cast = gm.RunCastSync(area, out o);
            Assert(!cast && o.Reason == "area-refused" && !slot.IsUsed, "AI: an area spell is refused (NpcRefusesArea, AI-001)", "" + o);
            sc.SpellSlots.Remove(slot);

            Assert(logs.Count == 0, "AI: the refusals log no combat-log line, as the old NPC executor did", string.Join(" | ", logs));
            Assert(gm.Harness_OpenCastCount == 0, "AI: every refused cast is ended");
        }
        finally
        {
            ScenarioHooks.CombatLog = oldLog;
        }
    }

    /// <summary>RunCastSync refuses a decider that may wait for the player: one cast-stack violation, false, no cast opened.</summary>
    private static void TestRunCastSyncRefusesUiDecider(GameManager gm, SpellcastingComponent sc, CharacterController caster, List<string> violations)
    {
        SpellCastRequest req = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out SpellSlot slot);
        req.Decider = UiCastDecider.Instance;
        int before = violations.Count;
        bool cast = gm.RunCastSync(req, out CastOutcome o);
        Assert(!cast && o.Reason == "not-synchronous" && violations.Count == before + 1 && gm.Harness_OpenCastCount == 0 && !slot.IsUsed,
            "RunCastSync refuses a decider that is not synchronous with one violation and opens no cast", o + " " + string.Join("; ", violations));
        violations.RemoveRange(before, violations.Count - before);
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// A synchronous decider that suspends at the choices anyway: RunCastSync ends the run, raises one cast-stack violation
    /// and returns false (Refused, nothing spent), so the NPC wrapper does not count a cast that never spent its slot.
    /// </summary>
    private static void TestRunCastSyncSuspendedRun(GameManager gm, SpellcastingComponent sc, CharacterController caster, List<string> violations)
    {
        var events = new List<string>();
        SpellCastRequest req = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out SpellSlot slot);
        var decider = new RecordingDecider(events);
        decider.Fills.Enqueue(CastChoiceFill.Suspended);
        req.Decider = decider;
        int before = violations.Count;
        bool cast = gm.RunCastSync(req, out CastOutcome o);
        Assert(!cast && o.Kind == CastOutcomeKind.Refused && o.At == CastStage.Legality && o.Reason == "not-synchronous" && !o.WasCast
               && violations.Count == before + 1 && gm.Harness_OpenCastCount == 0 && !slot.IsUsed && !caster.Actions.StandardActionUsed,
            "RunCastSync: a run suspended before its slot is spent raises one violation, is ended and returns false (Refused, nothing spent)",
            o + " " + string.Join("; ", violations));
        violations.RemoveRange(before, violations.Count - before);
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// An AI cast that runs while another cast's cancel snapshot is held (a PC cast waiting at its AoO prompt) leaves that
    /// snapshot as it was: the old NPC executor never touched it (batch 11 step 8 review).
    /// </summary>
    private static void TestAiCastKeepsOuterSnapshot(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var outer = new CastResourceSnapshot { Caster = ally, StandardActionUsed = true };
        CastResourceSnapshot saved = gm.Harness_PendingSpellcastSnapshot;
        gm.Harness_PendingSpellcastSnapshot = outer;
        try
        {
            SpellCastRequest req = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out SpellSlot slot);
            bool cast = gm.RunCastSync(req, out CastOutcome o);
            Assert(cast && o.Kind == CastOutcomeKind.Resolved && ReferenceEquals(gm.Harness_PendingSpellcastSnapshot, outer),
                "AI: a resolved cast leaves an outer cast's cancel snapshot in place", o + " snapshot kept: " + ReferenceEquals(gm.Harness_PendingSpellcastSnapshot, outer));
            sc.SpellSlots.Remove(slot);

            req = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out slot);
            caster.Actions.UseStandardAction();
            cast = gm.RunCastSync(req, out o);
            Assert(!cast && ReferenceEquals(gm.Harness_PendingSpellcastSnapshot, outer),
                "AI: a refused cast leaves an outer cast's cancel snapshot in place", "" + o);
            sc.SpellSlots.Remove(slot);
        }
        finally
        {
            gm.Harness_PendingSpellcastSnapshot = saved;
        }
    }

    /// <summary>
    /// The NPC Blink branches (NpcSpendsBeforeFailureChecks, NpcBlinkLabelsAndSilentPass): a blinking AI caster that fails
    /// its 20% check has already spent its slot (Spend before Failure), fails at Failure under the NPC dice label and still
    /// counts as cast; one that passes logs no "Blink spell check" line; an AI ray at a blinking target that misses fails at
    /// TargetBlink under the NPC label and counts as cast. Percentile dice forced through ScenarioHooks.RollFilter.
    /// </summary>
    private static void TestAiBlink(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var labels = new List<string>();
        var logs = new List<string>();
        int casterBlinkRoll = 10;
        int targetBlinkRoll = 10;
        Func<int, string, int, int> oldFilter = ScenarioHooks.RollFilter;
        Action<string> oldLog = ScenarioHooks.CombatLog;
        ScenarioHooks.RollFilter = (sides, label, natural) =>
        {
            if (label != null && label.StartsWith("Blink", StringComparison.Ordinal))
            {
                labels.Add(label);
                return label.Contains("caster") ? casterBlinkRoll : targetBlinkRoll;
            }
            return natural;
        };
        ScenarioHooks.CombatLog = m => logs.Add(m);
        ActiveSpellEffect casterBlink = AddBlink(caster);
        ActiveSpellEffect allyBlink = null;
        try
        {
            // The caster fails its Blink check: the slot is already spent.
            var events = new List<string>();
            SpellCastRequest req = BeginAi(sc, caster, caster, SpellNames.SHIELD, events, out SpellSlot slot);
            bool cast = gm.RunCastSync(req, out CastOutcome o, s => events.Add(s.ToString()));
            Assert(cast && o.Kind == CastOutcomeKind.Failed && o.At == CastStage.Failure && o.Reason == "blink-caster" && slot.IsUsed
                   && string.Join(",", events) == "Legality,decider:fill,Cost,Distraction,Spend,Failure,decider:finished:Failed@Failure",
                "AI: a blinking caster that fails its 20% has spent its slot first (Spend before Failure) and counts as cast",
                o + " " + string.Join(",", events));
            Assert(labels.Count == 1 && labels[0] == "Blink NPC caster spell failure",
                "AI: the caster's Blink check rolls under the NPC dice label", string.Join(",", labels));
            sc.SpellSlots.Remove(slot);

            // The caster passes: no "Blink spell check" line.
            casterBlinkRoll = 90;
            labels.Clear();
            logs.Clear();
            req = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out slot);
            cast = gm.RunCastSync(req, out o);
            Assert(cast && o.Kind == CastOutcomeKind.Resolved && labels.Count == 1 && !logs.Exists(l => l != null && l.Contains("Blink spell check")),
                "AI: a blinking caster that passes casts and logs no Blink check line (NpcBlinkLabelsAndSilentPass)", o + " " + string.Join(" | ", logs));
            sc.SpellSlots.Remove(slot);
            RemoveBlink(caster, casterBlink);
            casterBlink = null;

            // A ray at a blinking target misses: Failed at TargetBlink, the slot spent.
            allyBlink = AddBlink(ally);
            labels.Clear();
            req = BeginAi(sc, caster, ally, SpellNames.RAY_OF_FROST, null, out slot);
            int allyHp = ally.Stats.CurrentHP;
            cast = gm.RunCastSync(req, out o);
            Assert(cast && o.Kind == CastOutcomeKind.Failed && o.At == CastStage.TargetBlink && o.Reason == "target-blinked" && slot.IsUsed
                   && ally.Stats.CurrentHP == allyHp && labels.Count == 1 && labels[0] == "Blink NPC target spell failure",
                "AI: a ray at a blinking target that misses fails at TargetBlink under the NPC dice label and counts as cast",
                o + " " + string.Join(",", labels));
            sc.SpellSlots.Remove(slot);
            Assert(gm.Harness_OpenCastCount == 0, "AI: every Blink case's cast is ended");
        }
        finally
        {
            ScenarioHooks.RollFilter = oldFilter;
            ScenarioHooks.CombatLog = oldLog;
            RemoveBlink(caster, casterBlink);
            RemoveBlink(ally, allyBlink);
        }
    }

    private static ActiveSpellEffect AddBlink(CharacterController c)
    {
        StatusEffectManager statusMgr = c.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
        {
            statusMgr = c.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(c.Stats);
        }

        var effect = new ActiveSpellEffect
        {
            Spell = new SpellData { SpellId = SpellNames.BLINK, Name = "Blink" },
            CasterName = "UnitTest",
            CasterLevel = 5,
            RemainingRounds = 5,
            DurationType = DurationType.Rounds,
            AffectedCharacterName = c.Stats.CharacterName,
            IsApplied = true
        };
        statusMgr.ActiveEffects.Add(effect);
        return effect;
    }

    private static void RemoveBlink(CharacterController c, ActiveSpellEffect effect)
    {
        if (c == null || effect == null)
            return;
        StatusEffectManager statusMgr = c.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
            statusMgr.ActiveEffects.Remove(effect);
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

    /// <summary>
    /// Batch 11 step 9: area casts by the recording decider through the area resolve variant (no creature in the area, so
    /// nothing but the log and the spend comes of them). Skipped when a side is already out of the fight, since the area
    /// exit runs the combat-end check of the handlers that had one.
    /// </summary>
    private static void TestAreaCasts(GameManager gm, SpellcastingComponent sc, CharacterController caster)
    {
        CombatEndRules.SideCounts sides = gm.GetCombatEndSides();
        if (sides.PlayersOut || sides.EnemiesOut)
        {
            Debug.Log("  SKIP: the area casts (a side is out of the fight in this Play session, so their combat-end check would end it)");
            return;
        }

        GameManager.PlayerSubPhase subPhaseBefore = gm.CurrentSubPhase;
        string lastLogBefore = gm.Harness_LastCombatLog;

        RunArea(gm, sc, caster, SpellNames.BURNING_HANDS, false, true, "an area spell through the generic loop (Burning Hands)");
        RunArea(gm, sc, caster, SpellNames.BURNING_HANDS, true, true, "a quickened area spell through the generic loop (Burning Hands)");
        Assert(!SpellComponentRegistry.HasSpellComponentPouch(caster),
            "The test wizard carries no spell component pouch (so the Sleep cast below tests AreaSkipsPouch)");
        RunArea(gm, sc, caster, SpellNames.SLEEP, true, false, "a quickened Sleep (a handler that logs no prefix, SPL-161)");

        Assert(gm.CurrentSubPhase == subPhaseBefore, "No area stage set CurrentSubPhase", subPhaseBefore + " -> " + gm.CurrentSubPhase);
        Assert(gm.Harness_LastCombatLog == lastLogBefore, "No area stage wrote the last combat log (the UI decider's follow-up does)");
    }

    private static void RunArea(GameManager gm, SpellcastingComponent sc, CharacterController caster, string spellId, bool quickened,
        bool expectPrefixedLog, string label)
    {
        SpellData template = SpellDatabase.GetSpell(spellId);
        MetamagicData metamagic = null;
        int slotLevel = template.SpellLevel;
        if (quickened)
        {
            metamagic = new MetamagicData();
            metamagic.AppliedMetamagic.Add(MetamagicFeatId.QuickenSpell);
            slotLevel = metamagic.GetEffectiveSpellLevel(template.SpellLevel);
        }
        var slot = new SpellSlot(slotLevel, template.Clone(), false, "Wizard");
        sc.SpellSlots.Add(slot);
        caster.Actions.Reset();

        var events = new List<string>();
        var decider = new RecordingDecider(events);
        SpellCastRequest req = SpellCastRequest.ForUi(caster, template, metamagic, new CastSource());
        var cells = new HashSet<Vector2Int> { caster.GridPosition + new Vector2Int(0, 1) };
        SpellCastRequest.ForUiArea(req, new List<CharacterController>(), cells);
        req.Decider = decider;
        CastContext ctx = gm.BeginCast(req);
        ctx.StageObserver = stage => events.Add(stage.ToString());

        CastOutcome done = null;
        int doneCalls = 0;
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });

        Assert(string.Join(",", events) == string.Join(",", AreaResolvedSequence),
            label + ": the area cast runs the shared stages (no Ring of Counterspells), then the area Resolve and CombatEnd stages",
            string.Join(",", events));
        Assert(done != null && doneCalls == 1 && decider.Finishes == 1 && done.Kind == CastOutcomeKind.Resolved && done.At == CastStage.CombatEnd,
            label + ": FinishAreaCast and FinishCast give Resolved at CombatEnd once", "" + done);
        Assert(ctx.Ended && gm.Harness_OpenCastCount == 0, label + ": the cast is ended when it finishes");
        Assert(slot.IsUsed && (quickened || caster.Actions.StandardActionUsed),
            label + ": the slot is spent (and the standard action, unless quickened)", "slot used " + slot.IsUsed);
        Assert(ctx.AreaHandler != null && ctx.ResultLog != null, label + ": an area handler resolved the cast and built its log");
        if (ctx.ResultLog == null)
        {
            sc.SpellSlots.Remove(slot);
            return;
        }

        Assert(!quickened || ctx.LogPrefix.Contains("QUICKENED"), label + ": the Cost stage built the quickened prefix once", ctx.LogPrefix);
        if (expectPrefixedLog)
            Assert(ctx.ResultLog.StartsWith(ctx.LogPrefix, StringComparison.Ordinal) && ctx.ResultLog.Contains("No valid targets in area!"),
                label + ": the generic loop's log carries the cast's prefix", ctx.ResultLog);
        else
            Assert(!ctx.ResultLog.Contains("QUICKENED") && ctx.ResultLog.StartsWith("═", StringComparison.Ordinal) && template.HasMaterialComponent,
                label + ": the log has no prefix, and the material-component spell was cast without a pouch (AreaSkipsPouch)", ctx.ResultLog);
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
