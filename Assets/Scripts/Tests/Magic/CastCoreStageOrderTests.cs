#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
/// the stages in today's order (the Globe stage before Resolve since batch 11 step 11, on the area path before the
/// invisibility break; ConcentrationOnCast after the provocation answer since batch 11 step 12), ask the decider for the choices after the
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
/// violation; an AI cast leaves an outer cast's cancel snapshot in place; the AI's Blink rolls (the slot spent at Failure,
/// the shared dice labels and log since batch 11 step 13, a miss on a blinking target) hold with forced dice; the AI's
/// refusals log nothing; and the core's source names no decider type and never asks who controls the caster.
///
/// Batch 11 step 13 (raw-distraction-failure-stages): the component rules (CastComponentRules), the Silence refusal for
/// a PC and an AI caster (Silent Spell and a prepared silent spell allowed), the deafened roll once in the Failure stage
/// and never in SpellCaster.Cast, arcane spell failure only for a somatic spell and for a scroll, and the entangled
/// Concentration check and the blinking caster's 20% on the area and hold paths.
///
/// Batch 11 step 9 (cast-core-area): SpellCastRequest.ForUiArea sets CastResolveKind.Area, no single target and
/// LegacyCastQuirkSets.UiAreaPath (an explicit list: the UI path's bits an area cast shows plus the Area* bits, which no
/// other set carries). Area casts by the recording decider (a plain Burning Hands through the generic loop, a quickened
/// one, and a quickened Sleep, a material-component spell the pouchless wizard may cast because the area path skips the
/// pouch) run the shared stages without the Ring of Counterspells, then the area Resolve and CombatEnd stages, and finish
/// once, Resolved at CombatEnd, ended, with the slot and action spent; the generic loop's log carries the cast's
/// quickened prefix and Sleep's does not (SPL-161). Skipped when a side is already out (the area exit runs the
/// combat-end check).
///
/// Batch 11 step 10 (cast-core-summon-held-grease): SpellCastRequest.ForUiSummon, ForUiGrease and ForUiHoldCharge set the
/// Summon, Grease and HoldCharge resolve kinds, no target creature and LegacyCastQuirkSets.UiSummonPath, UiGreasePath and
/// UiHoldPath (explicit lists; the Summon*, Grease* and Hold* bits are each set by their own builder only); a held-charge
/// delivery carries DeliverySkipsCounterspell (UiPath); the AI decider casts a touch spell now. A hold (no choices asked,
/// the charge stored, Resolved at Resolve) and a Grease armor cast (Resolved at CombatEnd with its log) by the recording
/// decider run the shared stages and their resolve stage once each.
///
/// Batch 11 step 15 (raw-counterspell-order-parity): every cast runs Counterspell, InvisibilityBreak, then
/// RingCounterspell (decision D4), the AI path the PC path's stages exactly (ConcentrationOnCast included); the summon,
/// Grease and hold sets are the shared bit only; a Ring of Counterspells on an enemy absorbs a PC's and an NPC's spell
/// after the invisible caster's invisibility has ended, and does not counter an area spell that takes in its wearer; an
/// invisible caster's Grease on a foe's armor ends the invisibility and on an ally's does not; an area spell, Grease, a
/// summon and a hold are refused without a spell component pouch; a grappled caster cannot hold a somatic touch spell
/// (the pre-check logs nothing); a hold asks the caster's choices and keeps them with the charge; an AI caster's Silent
/// Image starts its concentration and a later AI cast runs the casting-while-concentrating step.
/// Review fixes: a Ring of Counterspells counters a held charge's delivery and the charge is gone; it does not meet a
/// burst centred on the caster (Prayer); a druid's M/DF Sleet Storm needs no pouch (the divine focus) and Earthquake has no
/// material component; a delivery is not tested for the pouch again; a hold suspended at its energy dialog resumes through
/// ResumeCastAfterChoiceDialog and its delivery request (GameManager.BuildHeldChargeDeliveryRequest) carries the energy.
/// </summary>
public static class CastCoreStageOrderTests
{
    private static int _passed;
    private static int _failed;

    /// <summary>The stages of a fully resolved single-target cast with the UI path's quirks, with the decider's calls.</summary>
    private static readonly string[] ResolvedSequence =
    {
        "Legality", "decider:fill", "Cost", "Distraction", "Failure", "Spend", "Provocation",
        "decider:provocation", "ConcentrationOnCast", "Counterspell", "InvisibilityBreak", "RingCounterspell", "MirrorImage", "TargetBlink",
        "Globe", "Resolve", "Handlers", "Buff", "PostCast", "CombatEnd", "decider:before-combat-end",
        "decider:finished:Resolved@CombatEnd"
    };

    /// <summary>
    /// Today's PC single-target path, bit by bit (batch 11 step 7). Step 8 adds the AI path's own set; NPC-only bits must
    /// never join this list (PC and NPC follow identical rules; only the decider differs).
    /// </summary>
    private static readonly LegacyCastQuirk[] PcPathBits =
    {
        LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath,
        LegacyCastQuirk.HeldChargeDeliveryIsFree,
    };

    /// <summary>
    /// The stages of a resolved area cast with the area path's quirks (batch 11 step 9), with the decider's calls. The
    /// area Globe stage runs before the invisibility break (batch 11 step 11: a foe inside a globe is not in the area); the
    /// RingCounterspell stage runs after the break (batch 11 step 15) and tests no creature of an area.
    /// </summary>
    private static readonly string[] AreaResolvedSequence =
    {
        "Legality", "decider:fill", "Cost", "Distraction", "Failure", "Spend", "Provocation",
        "decider:provocation", "ConcentrationOnCast", "Counterspell", "Globe", "InvisibilityBreak", "RingCounterspell", "Resolve", "CombatEnd", "decider:before-combat-end",
        "decider:finished:Resolved@CombatEnd"
    };

    /// <summary>Today's PC area path, bit by bit (batch 11 step 9).</summary>
    private static readonly LegacyCastQuirk[] AreaPathBits =
    {
        LegacyCastQuirk.AreaCombatEndOnlyInSomeHandlers,    // CORE-039
        LegacyCastQuirk.AreaLogPrefixOnlyInSomeHandlers,    // SPL-161
    };

    /// <summary>
    /// The bits by which the AI path's quirk set differs from the UI path's (batch 11 step 8), each naming a PC/NPC parity
    /// issue: the PC-only bit it drops and the NPC bits it adds (batch 11 step 14 removed the two range bits, SPL-017).
    /// </summary>
    private static readonly LegacyCastQuirk[] AiPathDifferences =
    {
        LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath,      // dropped: CORE-039 (the NPC wrapper checks after every cast)
        LegacyCastQuirk.NpcOwnLegalityChecks,                    // SPL-160
        LegacyCastQuirk.NpcRefusesArea,                          // AI-001
        LegacyCastQuirk.NpcNullMetamagic,                        // AI-001
        LegacyCastQuirk.NpcResultLogBeforeTargetDeath,           // SPL-160
        LegacyCastQuirk.NpcCombatEndCheckedByCaller,             // CORE-039
        LegacyCastQuirk.NpcNoMirrorImageSwap,                    // SPL-117
    };

    /// <summary>
    /// The AI path's stage sequence from the PC path's: the same stages in the same order since batch 11 step 15 removed
    /// the last NPC bits that moved or skipped one (NpcNoConcentrationOnCastCheck, NpcInvisibilityBreakBeforeCounterspell;
    /// step 13 removed the NPC path's Spend before Failure). Kept as a seam: an NPC bit that moved a stage would show here.
    /// </summary>
    private static List<string> DeriveAiSequence(IEnumerable<string> pcSequence)
    {
        return new List<string>(pcSequence);
    }

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== CAST CORE STAGE ORDER TESTS (batch 11 step 7) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        TestQuirkSets();
        TestEffectQuirkSets();
        TestComponentRules();
        TestRingAndFocusRules();
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

        public void ChooseTouchDelivery(GameManager gm, CastContext ctx, Action<CastTouchDelivery> answer)
        {
            Events.Add("decider:touch");
            answer(CastTouchDelivery.CastNow);
        }

        public void ChooseProvocation(GameManager gm, CastContext ctx, Action<CastProvocationAnswer> answer)
        {
            Events.Add("decider:provocation");
            if (HoldProvocation)
                PendingAnswer = answer;
            else
                answer(Answer);
        }

        /// <summary>Not asked by the cast core for the caster: a target's own decider answers (CastDeciders.For).</summary>
        public bool ForgoSave(CharacterController self, CharacterController caster, SpellData spell)
            => SpellSaveRules.IsHarmlessOrBeneficialTo(self, caster, spell);

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
        Assert(ui.HasQuirk(LegacyCastQuirk.EvaluateCombatEndOnlyOnTargetDeath | LegacyCastQuirk.HeldChargeDeliveryIsFree) && !ui.HasQuirk((LegacyCastQuirk)(1L << 62)),
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

            // Batch 11 step 13: one entangled, deafened, Silence, ASF and Blink rule on every path.
            TestDistractionAndFailureStages(gm, sc, caster, ally);
            Assert(violations.Count == 0, "No cast-core or cast-stack violation for the step 13 cases", string.Join("; ", violations));

            TestReentryWhileAwaitingProvocation(gm, sc, caster, ally, violations);
            TestMissingQuirk(gm, sc, caster, violations);
            TestVoluntarySave(gm, sc, caster, ally, violations);

            // Batch 11 step 9: area casts through the same core.
            int violationsBeforeArea = violations.Count;
            TestAreaCasts(gm, sc, caster);
            Assert(violations.Count == violationsBeforeArea, "No cast-core or cast-stack violation for area requests",
                string.Join("; ", violations.GetRange(violationsBeforeArea, violations.Count - violationsBeforeArea)));

            // Batch 11 step 10: a hold and a Grease cast through the same core.
            int violationsBeforeEffects = violations.Count;
            TestEffectCasts(gm, sc, caster, ally);
            Assert(violations.Count == violationsBeforeEffects, "No cast-core or cast-stack violation for hold and Grease requests",
                string.Join("; ", violations.GetRange(violationsBeforeEffects, violations.Count - violationsBeforeEffects)));

            // Batch 11 step 14: a failed spend refunds the action, and a hold of a cleric's conversion spends the sacrificed spell.
            int violationsBeforeRefund = violations.Count;
            TestFailedSpendRefund(gm, sc, caster, ally);
            TestConvertedHold(gm);
            Assert(violations.Count == violationsBeforeRefund, "No cast-core or cast-stack violation for the step 14 cases",
                string.Join("; ", violations.GetRange(violationsBeforeRefund, violations.Count - violationsBeforeRefund)));

            // Batch 11 step 15: one counterspell, ring, invisibility, pouch, grapple, choice and concentration rule.
            int violationsBeforeStep15 = violations.Count;
            TestRingAfterInvisibilityBreak(gm, sc, caster);
            TestRingCountersDelivery(gm, sc, caster);
            TestGreaseInvisibility(gm, sc, caster, ally);
            TestPouchOnEveryPath(gm, sc, caster, ally);
            TestHoldWhileGrappled(gm, sc, caster);
            TestHoldKeepsChoices(gm, sc, caster);
            TestHoldChoiceResume(gm, sc, caster);
            TestAiConcentration(gm, sc, caster, ally);
            TestPcCastEndsConcentration(gm, sc, caster, ally);
            TestDirectScrollEndsConcentration(gm, caster);
            Assert(violations.Count == violationsBeforeStep15, "No cast-core or cast-stack violation for the step 15 cases",
                string.Join("; ", violations.GetRange(violationsBeforeStep15, violations.Count - violationsBeforeStep15)));
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

        public void ChooseTouchDelivery(GameManager gm, CastContext ctx, Action<CastTouchDelivery> answer)
        {
            Events.Add("decider:touch");
            AiCastDecider.Instance.ChooseTouchDelivery(gm, ctx, answer);
        }

        public void ChooseProvocation(GameManager gm, CastContext ctx, Action<CastProvocationAnswer> answer)
        {
            Events.Add("decider:provocation");
            AiCastDecider.Instance.ChooseProvocation(gm, ctx, answer);
        }

        public bool ForgoSave(CharacterController self, CharacterController caster, SpellData spell)
            => AiCastDecider.Instance.ForgoSave(self, caster, spell);

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
        // The test wizard carries no component pouch (the request's own clone), as for the UI-path requests: since batch
        // 11 step 17 the AI path makes the component test too (SpellComponentRulesTests covers it).
        req.Spell.HasMaterialComponent = false;
        req.Spell.HasFocusComponent = false;
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
            label + ": the AI path runs the PC path's stages in the PC path's order (batch 11 step 15: ConcentrationOnCast, and Counterspell before InvisibilityBreak before RingCounterspell)",
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
                    "AI: a touch spell at a creature 40 squares away is refused by the caster-level range (CasterLevelRules)", "" + o);
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
    /// The Blink rolls on the AI path (batch 11 step 13: one rule, label and log for every caster, through
    /// SpellResolutionService): a blinking AI caster that fails its 20% check fails at Failure under the shared dice
    /// label, its slot spent there, and still counts as cast (the Spend stage is not entered); one that passes logs the
    /// "Blink spell check" line as a PC's does; an AI ray at a blinking target that misses fails at TargetBlink under the
    /// shared label and counts as cast. Percentile dice forced through ScenarioHooks.RollFilter.
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
            // The caster fails its Blink check: the slot is spent at Failure.
            var events = new List<string>();
            SpellCastRequest req = BeginAi(sc, caster, caster, SpellNames.SHIELD, events, out SpellSlot slot);
            bool cast = gm.RunCastSync(req, out CastOutcome o, s => events.Add(s.ToString()));
            Assert(cast && o.Kind == CastOutcomeKind.Failed && o.At == CastStage.Failure && o.Reason == "blink-caster" && slot.IsUsed
                   && string.Join(",", events) == "Legality,decider:fill,Cost,Distraction,Failure,decider:finished:Failed@Failure",
                "AI: a blinking caster that fails its 20% loses the spell at Failure, its slot spent there, and counts as cast",
                o + " " + string.Join(",", events));
            Assert(labels.Count == 1 && labels[0] == SpellResolutionService.BlinkCasterDiceContext,
                "AI: the caster's Blink check rolls under the shared dice label", string.Join(",", labels));
            sc.SpellSlots.Remove(slot);

            // The caster passes: the "Blink spell check" line, as for a PC.
            casterBlinkRoll = 90;
            labels.Clear();
            logs.Clear();
            req = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out slot);
            cast = gm.RunCastSync(req, out o);
            Assert(cast && o.Kind == CastOutcomeKind.Resolved && labels.Count == 1 && logs.Exists(l => l != null && l.Contains("Blink spell check")),
                "AI: a blinking caster that passes casts and logs the Blink check line, as a PC does", o + " " + string.Join(" | ", logs));
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
                   && ally.Stats.CurrentHP == allyHp && labels.Count == 1 && labels[0] == SpellResolutionService.BlinkTargetDiceContext,
                "AI: a ray at a blinking target that misses fails at TargetBlink under the shared dice label and counts as cast",
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

    /// <summary>
    /// Batch 11 step 15 review fixes, pure rules: a Ring of Counterspells meets every single-target cast except a burst or
    /// emanation centred on its caster (GameManager.RingMeetsSingleCast; DMG p.230, PHB p.175; delegated RAW reading
    /// 2026-10-14); the M/DF spells are marked as such and Earthquake (V, S, DF, PHB p.225) has no material component; a
    /// held charge's delivery source is recognised (CastComponentRules.IsHeldChargeDelivery).
    /// </summary>
    private static void TestRingAndFocusRules()
    {
        Assert(!GameManager.RingMeetsSingleCast(SpellDatabase.GetSpell(SpellNames.PRAYER))
               && !GameManager.RingMeetsSingleCast(SpellDatabase.GetSpell(SpellNames.INVISIBILITY_PURGE))
               && !GameManager.RingMeetsSingleCast(SpellDatabase.GetSpell(SpellNames.ZONE_OF_TRUTH)),
            "Step 15: a Ring of Counterspells does not meet a burst or emanation centred on the caster (Prayer, Invisibility Purge, Zone of Truth)");
        Assert(GameManager.RingMeetsSingleCast(SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE))
               && GameManager.RingMeetsSingleCast(SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP))
               && GameManager.RingMeetsSingleCast(SpellDatabase.GetSpell(SpellNames.SILENCE))
               && GameManager.RingMeetsSingleCast(SpellDatabase.GetSpell(SpellNames.SHIELD))
               && !GameManager.RingMeetsSingleCast(null),
            "Step 15: a Ring of Counterspells meets a targeted spell, a touch spell, a Silence centred on a creature and a personal spell");

        string[] materialOrFocus = { SpellNames.SLEET_STORM, SpellNames.WALL_OF_FIRE, SpellNames.SUNBURST, SpellNames.SPELL_TURNING, SpellNames.HOLD_MONSTER };
        bool allMarked = true;
        foreach (string id in materialOrFocus)
        {
            SpellData spell = SpellDatabase.GetSpell(id);
            allMarked &= spell != null && spell.HasMaterialComponent && spell.HasDivineFocus && spell.MaterialOrDivineFocus;
        }
        Assert(allMarked, "Step 15: Sleet Storm, Wall of Fire, Sunburst, Spell Turning and Hold Monster are marked M/DF (PHB p.175)");
        SpellData earthquake = SpellDatabase.GetSpell(SpellNames.EARTHQUAKE);
        Assert(earthquake != null && !earthquake.HasMaterialComponent && earthquake.HasDivineFocus && !earthquake.MaterialOrDivineFocus,
            "Step 15: Earthquake is V, S, DF with no material component (PHB p.225)");
        SpellData fireball = SpellDatabase.GetSpell(SpellNames.FIREBALL);
        Assert(fireball.HasMaterialComponent && !fireball.MaterialOrDivineFocus && !CastComponentRules.DivineFocusReplacesMaterial(null, fireball),
            "Step 15: a plain M spell (Fireball) is not M/DF");

        Assert(CastComponentRules.IsHeldChargeDelivery(new CastSource { Kind = CastSourceKind.HeldChargeDelivery })
               && !CastComponentRules.IsHeldChargeDelivery(new CastSource()) && !CastComponentRules.IsHeldChargeDelivery(null),
            "Step 15: a held charge's delivery source is recognised as no casting (PHB p.141)");
    }

    // ── Batch 11 step 13: one entangled, deafened, Silence, ASF and Blink rule on every path ─────────────────

    /// <summary>
    /// The pure component rules (CastComponentRules): Silent Spell and Still Spell remove their component, from the
    /// cast's metamagic or a prepared spell's own record; no spell is null-safe.
    /// </summary>
    private static void TestComponentRules()
    {
        SpellData shield = SpellDatabase.GetSpell(SpellNames.SHIELD).Clone();
        MetamagicData silent = new MetamagicData();
        silent.AppliedMetamagic.Add(MetamagicFeatId.SilentSpell);
        MetamagicData still = new MetamagicData();
        still.AppliedMetamagic.Add(MetamagicFeatId.StillSpell);
        Assert(CastComponentRules.HasVerbalComponent(shield, null) && CastComponentRules.HasSomaticComponent(shield, null),
            "Step 13: Shield has its verbal and somatic components (PHB p.278)");
        Assert(!CastComponentRules.HasVerbalComponent(shield, silent) && CastComponentRules.HasSomaticComponent(shield, silent),
            "Step 13: Silent Spell removes only the verbal component (PHB p.100)");
        Assert(CastComponentRules.HasVerbalComponent(shield, still) && !CastComponentRules.HasSomaticComponent(shield, still),
            "Step 13: Still Spell removes only the somatic component (PHB p.101)");
        SpellData preparedSilent = shield.Clone();
        preparedSilent.MetamagicDataRef = silent;
        Assert(!CastComponentRules.HasVerbalComponent(preparedSilent, null),
            "Step 13: a prepared silent spell's own metamagic record counts when the cast carries none");
        Assert(!CastComponentRules.HasVerbalComponent(null, null) && !CastComponentRules.HasSomaticComponent(null, null)
               && !CastComponentRules.SilenceBlocks(null, shield, null, null) && CastComponentRules.DeafenedFailureChance(null, shield, null, null) == 0
               && !CastComponentRules.ArcaneFailureApplies(null, shield, null, null),
            "Step 13: the component rules are null-safe");

        // Review of step 13: Combat Casting only on the defensive and grapple checks (PHB p.92), never the entangled one.
        Assert(!ConcentrationManager.CombatCastingApplies(ConcentrationCheckType.Entangled)
               && ConcentrationManager.CombatCastingApplies(ConcentrationCheckType.CastingDefensively)
               && ConcentrationManager.CombatCastingApplies(ConcentrationCheckType.Grappled),
            "Step 13: Combat Casting's +4 counts on casting defensively and grappled, not on the entangled check (PHB p.92)");

        // The casting distraction checks: a cast from a slot or a scroll (DMG p.238), not a wand use (spell trigger, no
        // casting) or a held-charge delivery (delegated RAW reading 2026-10-14).
        Assert(CastComponentRules.CastingDistractionApplies(null) && CastComponentRules.CastingDistractionApplies(new CastSource())
               && CastComponentRules.CastingDistractionApplies(new CastSource { Kind = CastSourceKind.Scroll })
               && !CastComponentRules.CastingDistractionApplies(new CastSource { Kind = CastSourceKind.Wand })
               && !CastComponentRules.CastingDistractionApplies(new CastSource { Kind = CastSourceKind.HeldChargeDelivery }),
            "Step 13: the entangled and grappled checks apply to a cast from a slot or a scroll, not to a wand use or a delivery");

        // A scroll's tradition is the scroll's own (DMG p.238), from ScrollData or the legacy ScrollType.
        Assert(CastComponentRules.IsArcaneScroll(new ItemData { IsScroll = true, ScrollType = "Arcane" })
               && !CastComponentRules.IsArcaneScroll(new ItemData { IsScroll = true, ScrollType = "Divine" })
               && CastComponentRules.IsArcaneScroll(new ItemData { IsScroll = true, ScrollType = "Divine", Scroll = new ScrollData { IsArcane = true } })
               && !CastComponentRules.IsArcaneScroll(null),
            "Step 13: a scroll is arcane by its ScrollData, else its ScrollType");

        // The entangled DC's level: a prepared heightened spell's own record raises it (PHB p.95).
        SpellData heightened = shield.Clone();
        var heighten = new MetamagicData();
        heighten.AppliedMetamagic.Add(MetamagicFeatId.HeightenSpell);
        heighten.HeightenToLevel = 3;
        heightened.MetamagicDataRef = heighten;
        Assert(GameManager.GetEntangledCastingSpellLevel(null, heightened, null) == 3
               && GameManager.GetEntangledCastingSpellLevel(null, shield, null) == 1,
            "Step 13: the entangled check's spell level is the heightened level of a prepared heightened spell (PHB p.95)",
            GameManager.GetEntangledCastingSpellLevel(null, heightened, null) + " / " + GameManager.GetEntangledCastingSpellLevel(null, shield, null));
    }

    /// <summary>
    /// Batch 11 step 13 in Play mode: the Silence refusal (PC and AI; Silent Spell, or a prepared silent spell, allowed;
    /// a wand refused; CheckLegality agrees), the deafened 20% once in the Failure stage on every path and never in
    /// SpellCaster.Cast, arcane spell failure only for a somatic spell and for a scroll, and the entangled Concentration
    /// check and the blinking caster's 20% on the area and hold paths.
    /// </summary>
    private static void TestDistractionAndFailureStages(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var labels = new List<string>();
        var logs = new List<string>();
        var forced = new Dictionary<string, int>();
        Func<int, string, int, int> oldFilter = ScenarioHooks.RollFilter;
        Action<string> oldLog = ScenarioHooks.CombatLog;
        ScenarioHooks.RollFilter = (sides, label, natural) =>
        {
            if (label != null)
                labels.Add(label);
            if (label != null && forced.TryGetValue(label, out int value))
                return value;
            if (label == null && forced.TryGetValue("d" + sides, out int any))
                return any;
            return natural;
        };
        ScenarioHooks.CombatLog = m => logs.Add(m);
        int asfBefore = caster.Stats.ArcaneSpellFailure;
        try
        {
            TestSilence(gm, sc, caster, logs);
            TestDeafened(gm, sc, caster, ally, labels, forced);
            TestArcaneFailureRule(gm, sc, caster, ally);
            TestEntangledAndBlinkOnEveryPath(gm, sc, caster, logs, labels, forced);
        }
        finally
        {
            ScenarioHooks.RollFilter = oldFilter;
            ScenarioHooks.CombatLog = oldLog;
            caster.Stats.SilenceActive = false;
            caster.Stats.ArcaneSpellFailure = asfBefore;
            caster.RemoveCondition(CombatConditionType.Deafened);
            caster.RemoveCondition(CombatConditionType.Entangled);
            if (gm.Harness_OpenCastCount > 0)
                gm.Harness_EndAllCasts("CastCoreStageOrderTests step 13 cleanup");
        }
    }

    private static MetamagicData Metamagic(MetamagicFeatId feat)
    {
        var m = new MetamagicData();
        m.AppliedMetamagic.Add(feat);
        return m;
    }

    private static void TestSilence(GameManager gm, SpellcastingComponent sc, CharacterController caster, List<string> logs)
    {
        caster.Stats.SilenceActive = true;
        caster.Stats.SilenceRoundsRemaining = 10;
        try
        {
            // PC: Shield (V, S) is refused at Legality, nothing spent, with a log line.
            logs.Clear();
            var events = new List<string>();
            var decider = new RecordingDecider(events);
            CastContext ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, decider, out SpellSlot slot);
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Refused && ctx.Outcome.Reason == "silenced" && ctx.Outcome.BackToActionMenu
                   && string.Join(",", events) == "Legality,decider:finished:Refused@Legality" && !slot.IsUsed && !caster.Actions.StandardActionUsed,
                "Step 13: a silenced PC caster is refused a verbal spell at Legality, before any choice, with nothing spent (PHB p.279)",
                ctx.Outcome + " " + string.Join(",", events));
            Assert(logs.Exists(l => l != null && l.Contains("inside a Silence")), "Step 13: the PC refusal is logged", string.Join(" | ", logs));
            sc.SpellSlots.Remove(slot);

            // PC: Silent Shield (a 2nd-level slot) is cast.
            SpellData shield = SpellDatabase.GetSpell(SpellNames.SHIELD);
            var silentSlot = new SpellSlot(2, shield.Clone(), false, "Wizard");
            sc.SpellSlots.Add(silentSlot);
            caster.Actions.Reset();
            events = new List<string>();
            decider = new RecordingDecider(events);
            SpellCastRequest req = SpellCastRequest.ForUi(caster, shield, Metamagic(MetamagicFeatId.SilentSpell), new CastSource());
            req.Spell.HasMaterialComponent = false;
            req.Aim.Target = caster;
            req.Decider = decider;
            ctx = gm.BeginCast(req);
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && silentSlot.IsUsed,
                "Step 13: a silenced PC caster casts a Silent Spell Shield from a 2nd-level slot (PHB p.100)", "" + ctx.Outcome);
            sc.SpellSlots.Remove(silentSlot);

            // CheckLegality agrees, with no side effect.
            caster.Actions.Reset();
            SpellCastRequest probe = SpellCastRequest.ForUi(caster, shield, null, new CastSource());
            probe.Aim.Target = caster;
            CastOutcome legality = gm.CheckLegality(probe);
            probe.Metamagic = Metamagic(MetamagicFeatId.SilentSpell);
            CastOutcome silentLegality = gm.CheckLegality(probe);
            Assert(legality.Reason == "silenced" && silentLegality.Kind == CastOutcomeKind.Resolved && !caster.Actions.StandardActionUsed,
                "Step 13: CheckLegality refuses the verbal cast and passes the silent one", legality + " / " + silentLegality);

            // A wand's command word cannot be spoken in a Silence; a scroll of a verbal spell cannot be read aloud either;
            // a held-charge delivery is no casting.
            Assert(CastComponentRules.SilenceBlocks(caster, shield, Metamagic(MetamagicFeatId.SilentSpell), new CastSource { Kind = CastSourceKind.Wand })
                   && CastComponentRules.SilenceBlocks(caster, shield, null, new CastSource { Kind = CastSourceKind.Scroll })
                   && !CastComponentRules.SilenceBlocks(caster, shield, null, new CastSource { Kind = CastSourceKind.HeldChargeDelivery }),
                "Step 13: Silence stops a wand (spoken trigger word, DMG p.213) and a verbal scroll, not a held-charge delivery (delegated RAW reading 2026-10-14)");

            // AI: refused silently, nothing spent.
            logs.Clear();
            SpellCastRequest ai = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out slot);
            bool cast = gm.RunCastSync(ai, out CastOutcome o);
            Assert(!cast && o.Reason == "silenced" && !slot.IsUsed && !caster.Actions.StandardActionUsed && logs.Count == 0,
                "Step 13: a silenced AI caster is refused a verbal spell (false, nothing spent, no log as the NPC path's other refusals)", "" + o);
            sc.SpellSlots.Remove(slot);

            // AI: a prepared Silent Spell Shield is cast (the NPC path drops a cast's metamagic, not the prepared record).
            var preparedSilent = new SpellSlot(2, false, "Wizard");
            preparedSilent.PrepareWithMetamagic(shield.Clone(), Metamagic(MetamagicFeatId.SilentSpell));
            sc.SpellSlots.Add(preparedSilent);
            caster.Actions.Reset();
            SpellData silentSpell = shield.Clone();
            silentSpell.MetamagicDataRef = Metamagic(MetamagicFeatId.SilentSpell);
            silentSpell.IsPreparedMetamagicSlotSpell = true; // the prepared slot's spell, as the menu builds it (batch 11 step 19)
            cast = gm.RunCastSync(SpellCastRequest.ForAI(caster, caster, silentSpell), out o);
            Assert(cast && o.Kind == CastOutcomeKind.Resolved && preparedSilent.IsUsed,
                "Step 13: a silenced AI caster casts its prepared Silent Spell Shield", "" + o);
            sc.SpellSlots.Remove(preparedSilent);
        }
        finally
        {
            caster.Stats.SilenceActive = false;
            caster.Stats.SilenceRoundsRemaining = 0;
        }
    }

    private static void TestDeafened(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally,
        List<string> labels, Dictionary<string, int> forced)
    {
        caster.ApplyCondition(CombatConditionType.Deafened, 10, "UnitTest");
        try
        {
            Assert(CastComponentRules.DeafenedFailureChance(caster, SpellDatabase.GetSpell(SpellNames.SHIELD), null, new CastSource()) == 20
                   && CastComponentRules.DeafenedFailureChance(caster, SpellDatabase.GetSpell(SpellNames.SHIELD), Metamagic(MetamagicFeatId.SilentSpell), null) == 0
                   && CastComponentRules.DeafenedFailureChance(caster, SpellDatabase.GetSpell(SpellNames.SHIELD), null, new CastSource { Kind = CastSourceKind.Scroll }) == 20
                   && CastComponentRules.DeafenedFailureChance(caster, SpellDatabase.GetSpell(SpellNames.SHIELD), null, new CastSource { Kind = CastSourceKind.Wand }) == 0
                   && CastComponentRules.DeafenedFailureChance(caster, SpellDatabase.GetSpell(SpellNames.SHIELD), null, new CastSource { Kind = CastSourceKind.HeldChargeDelivery }) == 0,
                "Step 13: a deafened caster's verbal spell has a 20% failure chance (PHB p.307), also from a scroll; none with Silent Spell, from a wand or for a delivery");

            // PC: the roll fails at Failure, once, the slot spent.
            forced[CastComponentRules.DeafenedFailureDiceContext] = 20;
            labels.Clear();
            var events = new List<string>();
            CastContext ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, new RecordingDecider(events), out SpellSlot slot);
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Failed && ctx.Outcome.At == CastStage.Failure && ctx.Outcome.Reason == "deafened"
                   && slot.IsUsed && labels.FindAll(l => l == CastComponentRules.DeafenedFailureDiceContext).Count == 1
                   && string.Join(",", events) == "Legality,decider:fill,Cost,Distraction,Failure,decider:finished:Failed@Failure",
                "Step 13: a deafened PC's verbal spell fails at the Failure stage on a d% of 20, one roll, the slot spent", ctx.Outcome + " " + string.Join(",", events));
            sc.SpellSlots.Remove(slot);

            // PC: a d% of 21 passes, and SpellCaster.Cast rolls nothing more (every unlabelled d% forced to 1).
            forced[CastComponentRules.DeafenedFailureDiceContext] = 21;
            forced["d100"] = 1;
            labels.Clear();
            ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, new RecordingDecider(new List<string>()), out slot);
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && ctx.Result != null && ctx.Result.Success
                   && labels.FindAll(l => l == CastComponentRules.DeafenedFailureDiceContext).Count == 1,
                "Step 13: a deafened PC who passes the d% casts; SpellCaster.Cast no longer rolls deafness (SPL-150)", ctx.Outcome + " " + ctx.Result?.NoEffectReason);
            sc.SpellSlots.Remove(slot);

            // AI: the same roll, the same failure.
            forced[CastComponentRules.DeafenedFailureDiceContext] = 5;
            labels.Clear();
            SpellCastRequest ai = BeginAi(sc, caster, caster, SpellNames.SHIELD, null, out slot);
            bool cast = gm.RunCastSync(ai, out CastOutcome o);
            Assert(cast && o.Kind == CastOutcomeKind.Failed && o.At == CastStage.Failure && o.Reason == "deafened" && slot.IsUsed
                   && labels.FindAll(l => l == CastComponentRules.DeafenedFailureDiceContext).Count == 1,
                "Step 13: a deafened AI caster's verbal spell fails at the same Failure stage (one roll; counts as cast)", "" + o);
            sc.SpellSlots.Remove(slot);

            // SpellCaster.Cast on its own never fails a deafened caster's spell (the Failure stage owns the roll).
            SpellResult direct = SpellCaster.Cast(SpellDatabase.GetSpell(SpellNames.SHIELD).Clone(), caster.Stats, caster.Stats, null, true, false, caster, caster);
            Assert(direct != null && (direct.NoEffectReason == null || !direct.NoEffectReason.ToLowerInvariant().Contains("deaf")),
                "Step 13: SpellCaster.Cast has no deafness roll of its own", direct?.NoEffectReason);
        }
        finally
        {
            forced.Clear();
            caster.RemoveCondition(CombatConditionType.Deafened);
        }
    }

    private static void TestArcaneFailureRule(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController fighter)
    {
        SpellData shield = SpellDatabase.GetSpell(SpellNames.SHIELD);
        caster.Stats.ArcaneSpellFailure = 100;
        Assert(caster.Stats.IsAffectedByArcaneSpellFailure, "Step 13: the test wizard is an arcane caster");
        Assert(CastComponentRules.ArcaneFailureApplies(caster, shield, null, new CastSource())
               && !CastComponentRules.ArcaneFailureApplies(caster, shield, Metamagic(MetamagicFeatId.StillSpell), new CastSource())
               && CastComponentRules.ArcaneFailureApplies(caster, shield, null, new CastSource { Kind = CastSourceKind.Scroll })
               && !CastComponentRules.ArcaneFailureApplies(caster, shield, null, new CastSource { Kind = CastSourceKind.Wand })
               && !CastComponentRules.ArcaneFailureApplies(caster, shield, null, new CastSource { Kind = CastSourceKind.HeldChargeDelivery })
               && !CastComponentRules.ArcaneFailureApplies(caster, shield, null, new CastSource { Kind = CastSourceKind.TestPanel, Bypass = TestPanelBypass.ArcaneFailure }),
            "Step 13: arcane spell failure applies to a somatic spell from a slot or a scroll (PHB p.122, DMG p.238), not with Still Spell, from a wand, for a delivery or with the F12 bypass");

        // A scroll's own tradition decides (DMG p.238), not the reader's class: a wizard reading a divine scroll rolls none,
        // a fighter reading an arcane one (Use Magic Device) does.
        var arcaneScroll = new ItemData { IsScroll = true, ScrollType = "Arcane" };
        var divineScroll = new ItemData { IsScroll = true, ScrollType = "Divine" };
        Assert(!CastComponentRules.ArcaneFailureApplies(caster, shield, null, new CastSource { Kind = CastSourceKind.Scroll, Item = divineScroll })
               && CastComponentRules.ArcaneFailureApplies(caster, shield, null, new CastSource { Kind = CastSourceKind.Scroll, Item = arcaneScroll })
               && !fighter.Stats.IsAffectedByArcaneSpellFailure
               && CastComponentRules.ArcaneFailureApplies(fighter, shield, null, new CastSource { Kind = CastSourceKind.Scroll, Item = arcaneScroll })
               && !CastComponentRules.ArcaneFailureApplies(fighter, shield, null, new CastSource { Kind = CastSourceKind.Scroll, Item = divineScroll })
               && !CastComponentRules.ArcaneFailureApplies(fighter, shield, null, new CastSource()),
            "Step 13: scroll arcane spell failure follows the scroll's tradition: none for a wizard reading a divine scroll, rolled for a fighter reading an arcane one (DMG p.238)");

        // A spell with no somatic component is cast at 100% arcane spell failure (SPL-096).
        var events = new List<string>();
        CastContext ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, new RecordingDecider(events), out SpellSlot slot);
        ctx.Request.Spell.HasSomaticComponent = false;
        gm.ExecuteCast(ctx, null);
        Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && slot.IsUsed,
            "Step 13: a spell with no somatic component rolls no arcane spell failure (PHB p.122; SPL-096)", "" + ctx.Outcome);
        sc.SpellSlots.Remove(slot);

        // The same spell with its somatic component fails at 100%.
        ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, new RecordingDecider(new List<string>()), out slot);
        gm.ExecuteCast(ctx, null);
        Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Failed && ctx.Outcome.Reason == "arcane-spell-failure" && slot.IsUsed,
            "Step 13: the somatic spell fails at 100% arcane spell failure, its slot spent", "" + ctx.Outcome);
        sc.SpellSlots.Remove(slot);
        caster.Stats.ArcaneSpellFailure = 0;
    }

    /// <summary>A UI area request (Burning Hands at one empty square) by <paramref name="decider"/>, with a fresh slot.</summary>
    private static CastContext BeginArea(GameManager gm, SpellcastingComponent sc, CharacterController caster, RecordingDecider decider, out SpellSlot slot)
    {
        SpellData template = SpellDatabase.GetSpell(SpellNames.BURNING_HANDS);
        slot = new SpellSlot(template.SpellLevel, template.Clone(), false, "Wizard");
        sc.SpellSlots.Add(slot);
        caster.Actions.Reset();
        sc.ResetQuickenedSpellTracking();
        SpellCastRequest req = SpellCastRequest.ForUi(caster, template, null, new CastSource());
        SpellCastRequest.ForUiArea(req, new List<CharacterController>(), new HashSet<Vector2Int> { caster.GridPosition + new Vector2Int(0, 1) });
        req.Decider = decider;
        CastContext ctx = gm.BeginCast(req);
        ctx.StageObserver = stage => decider.Events.Add(stage.ToString());
        return ctx;
    }

    private static void TestEntangledAndBlinkOnEveryPath(GameManager gm, SpellcastingComponent sc, CharacterController caster,
        List<string> logs, List<string> labels, Dictionary<string, int> forced)
    {
        caster.ApplyCondition(CombatConditionType.Entangled, 10, "UnitTest");
        // The caster takes Combat Casting for these checks: its +4 must not count on the entangled check (PHB p.92).
        bool addedCombatCasting = !caster.Stats.HasFeat("Combat Casting");
        if (addedCombatCasting)
            caster.Stats.Feats.Add("Combat Casting");
        int bonus = caster.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false);
        Assert(caster.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: true) == bonus + 4,
            "Step 13: the test caster has Combat Casting for the entangled checks");
        try
        {
            // A Shield without its somatic component still needs the check: Concentration, DC 15 + 1 = 16.
            forced[GameManager.EntangledConcentrationDiceContext] = 1;
            logs.Clear();
            labels.Clear();
            var events = new List<string>();
            CastContext ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, new RecordingDecider(events), out SpellSlot slot);
            ctx.Request.Spell.HasSomaticComponent = false;
            gm.ExecuteCast(ctx, null);
            bool lost = 1 + bonus < 16;
            Assert(labels.Contains(GameManager.EntangledConcentrationDiceContext)
                   && logs.Exists(l => l != null && l.Contains("d20 1 + conc " + bonus + " = " + (1 + bonus) + " vs DC 16")),
                "Step 13: an entangled caster makes a Concentration check (d20 + Concentration " + bonus + ", no Combat Casting, PHB p.92) against DC 15 + spell level 1, for a spell with no somatic component too (PHB p.308, p.70; SPL-145)",
                string.Join(" | ", logs));
            Assert(ctx.Outcome != null && (lost
                    ? ctx.Outcome.Kind == CastOutcomeKind.Interrupted && ctx.Outcome.At == CastStage.Distraction && ctx.Outcome.Reason == "entangled" && slot.IsUsed && !ctx.Outcome.ShowsActionMenu
                    : ctx.Outcome.Kind == CastOutcomeKind.Resolved),
                "Step 13: the entangled check's result decides the cast (a lost spell spends its slot)", "" + ctx.Outcome);
            sc.SpellSlots.Remove(slot);

            // Batch 11 step 14 review fix: a lost entangled check whose slot cannot be spent gives the action back
            // (LoseCastAtDistraction, as LoseCastAtFailure does): no spell was cast (PHB p.141, p.143).
            if (lost)
            {
                forced[GameManager.EntangledConcentrationDiceContext] = 1;
                ctx = Begin(gm, sc, caster, caster, SpellNames.SHIELD, new RecordingDecider(new List<string>()), out slot);
                ctx.Request.Spell.HasSomaticComponent = false;
                sc.SpellSlots.Remove(slot); // no slot holds the spell: the lost check has nothing to spend
                gm.ExecuteCast(ctx, null);
                Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Failed && ctx.Outcome.At == CastStage.Distraction
                       && ctx.Outcome.Reason == CastOutcome.ReasonSlotNotSpent
                       && !caster.Actions.StandardActionUsed && caster.Actions.HasStandardAction,
                    "Step 14: a lost entangled check with no slot to spend ends Failed at Distraction and gives the standard action back (SPL-147)",
                    ctx.Outcome + " / standard used " + caster.Actions.StandardActionUsed);
                Assert(gm.Harness_PendingSpellcastSnapshot == null, "Step 14: the entangled refund uses the snapshot up");
            }

            // The area path makes the check too (no AreaSkipsEntangled).
            forced[GameManager.EntangledConcentrationDiceContext] = 1;
            labels.Clear();
            events = new List<string>();
            ctx = BeginArea(gm, sc, caster, new RecordingDecider(events), out slot);
            gm.ExecuteCast(ctx, null);
            Assert(labels.Contains(GameManager.EntangledConcentrationDiceContext)
                   && (!lost || (ctx.Outcome != null && ctx.Outcome.At == CastStage.Distraction && ctx.Outcome.Reason == "entangled" && slot.IsUsed)),
                "Step 13: an entangled caster's area spell makes the entangled check (SPL-006)", ctx.Outcome + " " + string.Join(",", events));
            sc.SpellSlots.Remove(slot);

            // The hold makes it too (no HoldSkipsEntangled).
            labels.Clear();
            events = new List<string>();
            ctx = Begin(gm, sc, caster, null, SpellNames.SHOCKING_GRASP, new RecordingDecider(events), out slot);
            SpellCastRequest.ForUiHoldCharge(ctx.Request);
            gm.ExecuteCast(ctx, null);
            Assert(labels.Contains(GameManager.EntangledConcentrationDiceContext)
                   && (!lost || (ctx.Outcome != null && ctx.Outcome.At == CastStage.Distraction && ctx.Outcome.Reason == "entangled" && slot.IsUsed && !sc.HasHeldTouchCharge)),
                "Step 13: holding a touch charge makes the entangled check (SPL-146)", ctx.Outcome + " " + string.Join(",", events));
            if (sc.HasHeldTouchCharge)
                sc.ClearHeldTouchCharge("CastCoreStageOrderTests");
            sc.SpellSlots.Remove(slot);
        }
        finally
        {
            forced.Clear();
            caster.RemoveCondition(CombatConditionType.Entangled);
            if (addedCombatCasting)
                caster.Stats.Feats.Remove("Combat Casting");
        }

        // A blinking caster's area spell and hold have the 20% failure (no AreaSkipsBlink, HoldSkipsBlink).
        ActiveSpellEffect blink = AddBlink(caster);
        try
        {
            forced[SpellResolutionService.BlinkCasterDiceContext] = 10;
            var events = new List<string>();
            CastContext ctx = BeginArea(gm, sc, caster, new RecordingDecider(events), out SpellSlot slot);
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Failed && ctx.Outcome.At == CastStage.Failure && ctx.Outcome.Reason == "blink-caster"
                   && slot.IsUsed && !ctx.Outcome.ShowsActionMenu,
                "Step 13: a blinking caster's area spell can fail at the Failure stage (PHB p.206; SPL-006)", ctx.Outcome + " " + string.Join(",", events));
            sc.SpellSlots.Remove(slot);

            ctx = Begin(gm, sc, caster, null, SpellNames.SHOCKING_GRASP, new RecordingDecider(new List<string>()), out slot);
            SpellCastRequest.ForUiHoldCharge(ctx.Request);
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Failed && ctx.Outcome.Reason == "blink-caster" && slot.IsUsed && !sc.HasHeldTouchCharge,
                "Step 13: a blinking caster's hold of a touch charge can fail (PHB p.206; SPL-146)", "" + ctx.Outcome);
            sc.SpellSlots.Remove(slot);
        }
        finally
        {
            forced.Clear();
            RemoveBlink(caster, blink);
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

        Assert(string.Join(",", events) == "Legality,decider:fill,Cost,Distraction,Failure,Spend,Provocation,decider:provocation,decider:finished:Cancelled@Provocation",
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
        // The test wizard carries no spell component pouch: since batch 11 step 15 an area cast makes the pouch test
        // (TestPouchOnEveryPath), so this Sleep's own clone drops its material component.
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
        sc.ResetQuickenedSpellTracking();

        var events = new List<string>();
        var decider = new RecordingDecider(events);
        SpellCastRequest req = SpellCastRequest.ForUi(caster, template, metamagic, new CastSource());
        req.Spell.HasMaterialComponent = false; // the test wizard carries no component pouch (the request's own clone)
        var cells = new HashSet<Vector2Int> { caster.GridPosition + new Vector2Int(0, 1) };
        SpellCastRequest.ForUiArea(req, new List<CharacterController>(), cells);
        req.Decider = decider;
        CastContext ctx = gm.BeginCast(req);
        ctx.StageObserver = stage => events.Add(stage.ToString());

        CastOutcome done = null;
        int doneCalls = 0;
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });

        // A quickened cast does not provoke (PHB p.98, batch 11 step 12): the decider is not asked.
        var expected = new List<string>(AreaResolvedSequence);
        if (quickened)
            expected.Remove("decider:provocation");
        Assert(string.Join(",", events) == string.Join(",", expected),
            label + ": the area cast runs the shared stages (no provocation question when quickened), then the area Resolve and CombatEnd stages",
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
            Assert(!ctx.ResultLog.Contains("QUICKENED") && ctx.ResultLog.StartsWith("═", StringComparison.Ordinal),
                label + ": the log has no prefix", ctx.ResultLog);
        sc.SpellSlots.Remove(slot);
    }

    // ── Batch 11 step 10: the Summon, Grease and HoldCharge resolve variants ──────────────────────────────────

    /// <summary>
    /// The stages of a hold (Discharge Later): the choices asked (batch 11 step 15), the shared stages, Resolve ends the
    /// cast.
    /// </summary>
    private static readonly string[] HoldResolvedSequence =
    {
        "Legality", "decider:fill", "Cost", "Distraction", "Failure", "Spend", "Provocation", "decider:provocation",
        "ConcentrationOnCast", "Counterspell", "InvisibilityBreak", "RingCounterspell", "Resolve", "decider:finished:Resolved@Resolve"
    };

    /// <summary>The stages of a Grease cast with the Grease path's quirks: the shared stages, Resolve, then CombatEnd.</summary>
    private static readonly string[] GreaseResolvedSequence =
    {
        "Legality", "decider:fill", "Cost", "Distraction", "Failure", "Spend", "Provocation",
        "decider:provocation", "ConcentrationOnCast", "Counterspell", "InvisibilityBreak", "RingCounterspell", "Resolve", "CombatEnd",
        "decider:before-combat-end", "decider:finished:Resolved@CombatEnd"
    };

    /// <summary>The bits every summon, Grease and hold shows (LegacyCastQuirkSets.UiEffectPathShared).</summary>
    private static readonly LegacyCastQuirk[] EffectSharedBits =
    {
        LegacyCastQuirk.EffectResolveSkipsCombatEndCheck,   // CORE-039
    };

    /// <summary>
    /// Today's PC summon path, beyond the shared bits: none since batch 11 step 15 removed SummonSkipsCounterspell and
    /// SummonSkipsPouch (SPL-006).
    /// </summary>
    private static readonly LegacyCastQuirk[] SummonPathBits = { };

    /// <summary>
    /// Today's PC Grease path, beyond the shared bits: none since batch 11 step 15 removed GreaseSkipsCounterspell,
    /// GreaseSkipsPouch and GreaseSkipsInvisibilityBreak (SPL-006).
    /// </summary>
    private static readonly LegacyCastQuirk[] GreasePathBits = { };

    /// <summary>
    /// Today's PC hold path, beyond the shared bits: none since batch 11 step 15 removed HoldSkipsGrappleLegality,
    /// HoldSkipsPouch, HoldSkipsCounterspell and HoldSkipsChoices (SPL-146).
    /// </summary>
    private static readonly LegacyCastQuirk[] HoldPathBits = { };

    private static LegacyCastQuirk Union(params LegacyCastQuirk[][] lists)
    {
        LegacyCastQuirk all = LegacyCastQuirk.None;
        foreach (LegacyCastQuirk[] list in lists)
            foreach (LegacyCastQuirk bit in list)
            {
                Assert(Enum.IsDefined(typeof(LegacyCastQuirk), bit) && bit != LegacyCastQuirk.None, "The bit " + bit + " is one named flag");
                all |= bit;
            }
        return all;
    }

    /// <summary>The summon, Grease and hold builders and their quirk sets (batch 11 step 10).</summary>
    private static void TestEffectQuirkSets()
    {
        SpellData summon = SpellDatabase.GetSpell(SpellNames.SUMMON_MONSTER_1);
        SpellCastRequest s = SpellCastRequest.ForUiSummon(SpellCastRequest.ForUi(null, summon, null, null), new Vector2Int(3, 4));
        Assert(s.ResolveKind == CastResolveKind.Summon && s.Aim.Target == null && s.Aim.Destination == new Vector2Int(3, 4)
               && s.Quirks == LegacyCastQuirkSets.UiSummonPath,
            "ForUiSummon makes a summon request aimed at its destination, with no target creature and the summon path's quirks", "" + s.Quirks);
        Assert(LegacyCastQuirkSets.UiSummonPath == Union(EffectSharedBits, SummonPathBits),
            "The summon path's quirk set is exactly today's summon-path bits", "" + LegacyCastQuirkSets.UiSummonPath);

        SpellData grease = SpellDatabase.GetSpell(SpellNames.GREASE);
        SpellCastRequest g = SpellCastRequest.ForUi(null, grease, null, null);
        g.Choices.GreaseMode = GreaseCastMode.Armor;
        SpellCastRequest.ForUiGrease(g, null, null, null);
        Assert(g.ResolveKind == CastResolveKind.Grease && g.Aim.Target == null && g.Choices.GreaseMode == GreaseCastMode.Armor
               && g.Quirks == LegacyCastQuirkSets.UiGreasePath,
            "ForUiGrease makes one Grease request that keeps its mode choice, with the Grease path's quirks", "" + g.Quirks);
        Assert(LegacyCastQuirkSets.UiGreasePath == Union(EffectSharedBits, GreasePathBits),
            "The Grease path's quirk set is exactly today's Grease-path bits", "" + LegacyCastQuirkSets.UiGreasePath);

        SpellData grasp = SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP);
        SpellCastRequest h = SpellCastRequest.ForUiHoldCharge(SpellCastRequest.ForUi(null, grasp, null, new CastSource { Kind = CastSourceKind.SpontaneousSlot }));
        SpellCastRequest hc = SpellCastRequest.ForUiHoldCharge(SpellCastRequest.ForUi(null, grasp, null,
            new CastSource { Kind = CastSourceKind.SpontaneousConversion, ConversionLevel = 1 }));
        Assert(h.ResolveKind == CastResolveKind.HoldCharge && h.Source.Kind == CastSourceKind.HeldChargeHold && h.Aim.Target == null
               && h.Quirks == LegacyCastQuirkSets.UiHoldPath && hc.Source.Kind == CastSourceKind.SpontaneousConversion,
            "ForUiHoldCharge makes a hold (a slot's source becomes HeldChargeHold, a conversion keeps its kind and spends the sacrificed spell, SPL-154) with the hold path's quirks",
            h.Source.Kind + " / " + hc.Source.Kind);
        Assert(LegacyCastQuirkSets.UiHoldPath == Union(EffectSharedBits, HoldPathBits),
            "The hold path's quirk set is exactly today's hold-path bits", "" + LegacyCastQuirkSets.UiHoldPath);

        foreach (string name in Enum.GetNames(typeof(LegacyCastQuirk)))
        {
            var bit = (LegacyCastQuirk)Enum.Parse(typeof(LegacyCastQuirk), name);
            LegacyCastQuirk owner = name.StartsWith("Summon", StringComparison.Ordinal) ? LegacyCastQuirkSets.UiSummonPath
                : name.StartsWith("Grease", StringComparison.Ordinal) ? LegacyCastQuirkSets.UiGreasePath
                : name.StartsWith("Hold", StringComparison.Ordinal) ? LegacyCastQuirkSets.UiHoldPath
                : LegacyCastQuirk.None;
            if (owner == LegacyCastQuirk.None)
                continue;
            LegacyCastQuirk others = (LegacyCastQuirkSets.UiPath | LegacyCastQuirkSets.AiPath | LegacyCastQuirkSets.UiAreaPath
                                      | LegacyCastQuirkSets.UiSummonPath | LegacyCastQuirkSets.UiGreasePath | LegacyCastQuirkSets.UiHoldPath) & ~owner;
            Assert((owner & bit) == bit && (others & bit) == 0, "The bit " + name + " is set by its own builder only");
        }
        foreach (string name in new[] { "NpcNoConcentrationOnCastCheck", "NpcNoConcentrationTracking", "NpcInvisibilityBreakBeforeCounterspell",
                     "AreaSkipsRing", "AreaSkipsPouch", "SummonSkipsCounterspell", "SummonSkipsPouch", "GreaseSkipsCounterspell",
                     "GreaseSkipsPouch", "GreaseSkipsInvisibilityBreak", "HoldSkipsGrappleLegality", "HoldSkipsPouch",
                     "HoldSkipsCounterspell", "HoldSkipsChoices", "DeliverySkipsCounterspell" })
            Assert(!Enum.IsDefined(typeof(LegacyCastQuirk), name), "Batch 11 step 15 removed the quirk bit " + name);

        var touch = new List<CastTouchDelivery>();
        AiCastDecider.Instance.ChooseTouchDelivery(null, null, touch.Add);
        Assert(touch.Count == 1 && touch[0] == CastTouchDelivery.CastNow, "The AI decider casts a touch spell now (it never holds a charge, SPL-115)",
            string.Join(",", touch));
    }

    /// <summary>
    /// A hold and a Grease cast by the recording decider: each runs the shared stages with its path's skips, then its
    /// resolve stage, and finishes once with the slot and the standard action spent. The hold asks no choices, stores the
    /// charge with the cast's metamagic and ends at Resolve; the Grease armor cast greases the ally's armor and ends at
    /// CombatEnd with its result log. Summons need the grid and spawning, so the scenarios cover them
    /// (rules/cast-ui-summon-*).
    /// </summary>
    private static void TestEffectCasts(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        // The hold.
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        CastContext ctx = Begin(gm, sc, caster, null, SpellNames.SHOCKING_GRASP, decider, out SpellSlot slot);
        SpellCastRequest.ForUiHoldCharge(ctx.Request);
        CastOutcome done = null;
        int doneCalls = 0;
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });
        Assert(string.Join(",", events) == string.Join(",", HoldResolvedSequence),
            "A hold asks the caster's choices and runs the shared stages, then the HoldCharge Resolve stage (batch 11 step 15)", string.Join(",", events));
        Assert(done != null && doneCalls == 1 && decider.Finishes == 1 && done.Kind == CastOutcomeKind.Resolved && done.At == CastStage.Resolve
               && ctx.Ended && gm.Harness_OpenCastCount == 0,
            "A hold finishes once, Resolved at Resolve, and its cast is ended", "" + done);
        Assert(slot.IsUsed && caster.Actions.StandardActionUsed && sc.HasHeldTouchCharge && sc.HeldTouchSpell != null
               && sc.HeldTouchSpell.SpellId == SpellNames.SHOCKING_GRASP,
            "A hold spends the slot and the standard action and holds the charge (PHB p.176)");
        sc.ClearHeldTouchCharge("CastCoreStageOrderTests");
        sc.SpellSlots.Remove(slot);

        // Grease on the ally's worn armor.
        events = new List<string>();
        decider = new RecordingDecider(events);
        ctx = Begin(gm, sc, caster, null, SpellNames.GREASE, decider, out slot);
        ctx.Request.Choices.GreaseMode = GreaseCastMode.Armor;
        SpellCastRequest.ForUiGrease(ctx.Request, ally, null, null);
        done = null;
        doneCalls = 0;
        gm.ExecuteCast(ctx, o => { done = o; doneCalls++; });
        Assert(string.Join(",", events) == string.Join(",", GreaseResolvedSequence),
            "A Grease cast runs the shared stages, then the Grease Resolve and CombatEnd stages", string.Join(",", events));
        Assert(done != null && doneCalls == 1 && decider.Finishes == 1 && done.Kind == CastOutcomeKind.Resolved && done.At == CastStage.CombatEnd
               && !done.CombatEnded && ctx.Ended && gm.Harness_OpenCastCount == 0,
            "A Grease cast finishes once, Resolved at CombatEnd (no combat-end check), and its cast is ended", "" + done);
        StatusEffectManager allyEffects = ally.StatusEffectManager;
        Assert(slot.IsUsed && caster.Actions.StandardActionUsed && ctx.ResultLog != null && ctx.ResultLog.Contains("armor")
               && allyEffects != null && allyEffects.ActiveEffects.Exists(e => e != null && e.GreasedArmorGrappleEscapeBonus == 10),
            "A Grease armor cast spends the slot and the action and greases the ally's armor (+10 to escape a grapple, PHB p.237)", ctx.ResultLog);
        if (allyEffects != null)
            allyEffects.ActiveEffects.RemoveAll(e => e != null && e.Spell != null && e.Spell.SpellId == "grease_armor");
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// Batch 11 step 14 (SPL-147): a cast whose slot cannot be spent after its standard action was committed ends Failed at
    /// Spend and gives the action back (PHB p.141, p.143: casting the spell is the action); the old SlotFailureClearsSnapshot
    /// bit kept the action spent.
    /// </summary>
    private static void TestFailedSpendRefund(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        CastContext ctx = Begin(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, decider, out SpellSlot slot);
        sc.SpellSlots.Remove(slot); // no slot holds the spell: the spend fails after the Cost stage committed the action
        CastOutcome done = null;
        gm.ExecuteCast(ctx, o => done = o);
        Assert(done != null && done.Kind == CastOutcomeKind.Failed && done.At == CastStage.Spend && done.Reason == CastOutcome.ReasonSlotNotSpent,
            "A cast whose slot cannot be spent ends Failed at Spend", "" + done);
        Assert(!caster.Actions.StandardActionUsed && caster.Actions.HasStandardAction,
            "A failed spend gives the standard action back (SPL-147)", "standard used " + caster.Actions.StandardActionUsed);
        Assert(gm.Harness_PendingSpellcastSnapshot == null, "The refund uses the snapshot up");
    }

    /// <summary>
    /// Batch 11 step 14 (SPL-154): a good cleric 5 converts a prepared Bless into Cure Light Wounds (PHB p.32) and holds the
    /// charge (PHB p.141): the hold spends the Bless slot and the standard action and holds Cure Light Wounds. A second
    /// converted hold with no Bless left fails its spend and gives the action back (SPL-147).
    /// </summary>
    private static void TestConvertedHold(GameManager gm)
    {
        CharacterController cleric = null;
        try
        {
            cleric = TestHelpers.CreateCleric("CastCoreCleric", 5);
            cleric.SetTeam(CharacterTeam.Player);
            cleric.Stats.SpontaneousCasting = SpontaneousCastingType.Cure;
            SpellcastingComponent sc = cleric.gameObject.AddComponent<SpellcastingComponent>();
            sc.Init(cleric.Stats);
            sc.SpellSlots.Clear();
            var bless = new SpellSlot(1, SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), false, "Cleric");
            sc.SpellSlots.Add(bless);
            cleric.Actions.Reset();

            CastContext ctx = BeginConvertedHold(gm, cleric, out RecordingDecider decider);
            CastOutcome done = null;
            gm.ExecuteCast(ctx, o => done = o);
            Assert(done != null && done.Kind == CastOutcomeKind.Resolved && bless.IsUsed && cleric.Actions.StandardActionUsed
                   && sc.HasHeldTouchCharge && sc.HeldTouchSpell != null && sc.HeldTouchSpell.SpellId == SpellNames.CURE_LIGHT_WOUNDS,
                "A held converted Cure Light Wounds spends the sacrificed Bless and the standard action and holds the charge (SPL-154)",
                done + " bless used " + bless.IsUsed + " held " + sc.HasHeldTouchCharge);
            sc.ClearHeldTouchCharge("CastCoreStageOrderTests");

            cleric.Actions.Reset();
            ctx = BeginConvertedHold(gm, cleric, out decider);
            done = null;
            gm.ExecuteCast(ctx, o => done = o);
            Assert(done != null && done.Kind == CastOutcomeKind.Failed && done.Reason == CastOutcome.ReasonSlotNotSpent
                   && !cleric.Actions.StandardActionUsed && !sc.HasHeldTouchCharge,
                "A converted hold with no Bless left fails its spend, holds nothing and gives the action back (SPL-147)",
                done + " standard used " + cleric.Actions.StandardActionUsed);
        }
        finally
        {
            if (gm.Harness_OpenCastCount > 0)
                gm.Harness_EndAllCasts("CastCoreStageOrderTests converted hold cleanup");
            if (cleric != null)
                Object.DestroyImmediate(cleric.gameObject);
        }
    }

    private static CastContext BeginConvertedHold(GameManager gm, CharacterController cleric, out RecordingDecider decider)
    {
        decider = new RecordingDecider(new List<string>());
        SpellCastRequest req = SpellCastRequest.ForUi(cleric, SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS), null,
            new CastSource { Kind = CastSourceKind.SpontaneousConversion, ConversionLevel = 1, SacrificedSpellId = SpellNames.BLESS });
        req.Spell.HasMaterialComponent = false;
        req.Decider = decider;
        SpellCastRequest.ForUiHoldCharge(req);
        return gm.BeginCast(req);
    }

    // ── Batch 11 step 15: counterspell, ring, invisibility, pouch, grapple, choices and concentration ─────────────

    /// <summary>An enemy test creature 1 square from the caster with high hit points (it is never killed here).</summary>
    private static CharacterController CreateEnemy(string name, Vector2Int at)
    {
        CharacterController foe = TestHelpers.CreateCharacter(name, "Fighter", level: 20, con: 30, bab: 20, gridPosition: at);
        foe.SetTeam(CharacterTeam.Enemy);
        return foe;
    }

    /// <summary>A Ring of Counterspells holding <paramref name="spellId"/> on <paramref name="wearer"/>'s left hand (DMG p.230).</summary>
    private static ItemData WearCounterspellRing(CharacterController wearer, string spellId)
    {
        var ring = new ItemData
        {
            Id = RingNames.RING_OF_COUNTERSPELLS,
            Name = "Ring of Counterspells",
            IsRing = true,
            RingId = RingNames.RING_OF_COUNTERSPELLS,
            RingCounterspellStored = spellId,
            RingCounterspellStoredName = spellId,
            RingCounterspellStoredLevel = 1
        };
        global::Inventory inv = wearer.Inventory != null ? wearer.Inventory.GetInventory() : null;
        if (inv != null)
            inv.LeftRingSlot = ring;
        return ring;
    }

    /// <summary>
    /// Decision D4 (batch 11 step 15, SPL-118): a Ring of Counterspells absorbs a spell cast on its wearer after the
    /// hostile-cast invisibility break, so the invisible caster's invisibility ends, for a PC (the recording decider on the
    /// UI path) and an NPC (RunCastSync on the AI path) alike; the stages run Counterspell, InvisibilityBreak, then
    /// RingCounterspell. An area spell that takes in the wearer is not "cast upon" it (PHB p.175, DMG p.230; delegated RAW
    /// reading 2026-10-14): the ring keeps its spell and the area resolves.
    /// </summary>
    private static void TestRingAfterInvisibilityBreak(GameManager gm, SpellcastingComponent sc, CharacterController caster)
    {
        CharacterController foe = null;
        try
        {
            foe = CreateEnemy("CastCoreRingFoe", caster.GridPosition + new Vector2Int(2, 0));
            ItemData ring = WearCounterspellRing(foe, SpellNames.MAGIC_MISSILE);

            // PC path.
            caster.ApplyInvisibilityEffect(5, caster);
            var events = new List<string>();
            CastContext ctx = Begin(gm, sc, caster, foe, SpellNames.MAGIC_MISSILE, new RecordingDecider(events), out SpellSlot slot);
            int hpBefore = foe.Stats.CurrentHP;
            gm.ExecuteCast(ctx, null);
            string seq = string.Join(",", events);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Failed && ctx.Outcome.At == CastStage.RingCounterspell
                   && slot.IsUsed && foe.Stats.CurrentHP == hpBefore && string.IsNullOrEmpty(ring.RingCounterspellStored),
                "Step 15: a Ring of Counterspells on the target absorbs the PC's Magic Missile (DMG p.230): Failed at RingCounterspell, the slot spent, no damage, the ring empty",
                ctx.Outcome + " hp " + hpBefore + "->" + foe.Stats.CurrentHP + " ring '" + ring.RingCounterspellStored + "'");
            Assert(seq.Contains("Counterspell,InvisibilityBreak,RingCounterspell,decider:finished:Failed@RingCounterspell"),
                "Step 15: the PC cast runs Counterspell, InvisibilityBreak, then RingCounterspell (decision D4)", seq);
            Assert(!caster.HasActiveInvisibilityEffect,
                "Step 15: the invisible PC caster's invisibility ends although the ring absorbed its spell (decision D4, PHB p.245)");
            sc.SpellSlots.Remove(slot);

            // AI path.
            caster.ApplyInvisibilityEffect(5, caster);
            ring.RingCounterspellStored = SpellNames.MAGIC_MISSILE;
            var aiEvents = new List<string>();
            SpellCastRequest ai = BeginAi(sc, caster, foe, SpellNames.MAGIC_MISSILE, aiEvents, out slot);
            bool cast = gm.RunCastSync(ai, out CastOutcome o, stage => aiEvents.Add(stage.ToString()));
            Assert(cast && o.Kind == CastOutcomeKind.Failed && o.At == CastStage.RingCounterspell && slot.IsUsed
                   && string.Join(",", aiEvents).Contains("Counterspell,InvisibilityBreak,RingCounterspell"),
                "Step 15: the ring absorbs the NPC's Magic Missile in the same stage order (counts as cast)", o + " " + string.Join(",", aiEvents));
            Assert(!caster.HasActiveInvisibilityEffect,
                "Step 15: the invisible NPC caster's invisibility ends too: one rule for PCs and NPCs (SPL-118)");
            sc.SpellSlots.Remove(slot);

            // An area spell that takes in the wearer: the ring does not act (delegated RAW reading 2026-10-14).
            CombatEndRules.SideCounts sides = gm.GetCombatEndSides();
            if (!sides.PlayersOut && !sides.EnemiesOut)
            {
                ring.RingCounterspellStored = SpellNames.BURNING_HANDS;
                var areaEvents = new List<string>();
                ctx = BeginArea(gm, sc, caster, new RecordingDecider(areaEvents), out slot);
                ctx.Request.Aim.AreaTargets.Add(foe);
                ctx.Request.Aim.Cells.Add(foe.GridPosition);
                gm.ExecuteCast(ctx, null);
                Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && ring.RingCounterspellStored == SpellNames.BURNING_HANDS
                       && string.Join(",", areaEvents).Contains("InvisibilityBreak,RingCounterspell,Resolve"),
                    "Step 15: a Ring of Counterspells does not counter an area spell that takes in its wearer, which is not cast upon it (PHB p.175, DMG p.230; delegated RAW reading 2026-10-14)",
                    ctx.Outcome + " ring '" + ring.RingCounterspellStored + "' " + string.Join(",", areaEvents));
                sc.SpellSlots.Remove(slot);
            }
        }
        finally
        {
            caster.ForceEndInvisibility("CastCoreStageOrderTests");
            if (foe != null)
                Object.DestroyImmediate(foe.gameObject);
        }
    }

    /// <summary>
    /// A held charge delivered to a Ring of Counterspells wearer whose ring stores that spell is countered before the touch
    /// attack ("immediately, as a counterspell action", DMG p.230), and the countered charge is gone (PHB p.170-171; a
    /// delegated RAW reading 2026-10-14): Failed at RingCounterspell, no charge held, the ring empty, no damage.
    /// </summary>
    private static void TestRingCountersDelivery(GameManager gm, SpellcastingComponent sc, CharacterController caster)
    {
        CharacterController foe = null;
        try
        {
            foe = CreateEnemy("CastCoreRingDeliveryFoe", caster.GridPosition + new Vector2Int(1, 1));
            ItemData ring = WearCounterspellRing(foe, SpellNames.SHOCKING_GRASP);
            SpellData held = SpellCastRequest.CloneForCast(SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP));
            held.HasMaterialComponent = false;
            sc.SetHeldTouchCharge(held, null);
            caster.Actions.Reset();

            var events = new List<string>();
            SpellCastRequest req = GameManager.BuildHeldChargeDeliveryRequest(caster, sc);
            req.Aim.Target = foe;
            req.Decider = new RecordingDecider(events);
            CastContext ctx = gm.BeginCast(req);
            ctx.StageObserver = stage => events.Add(stage.ToString());
            int hpBefore = foe.Stats.CurrentHP;
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Failed && ctx.Outcome.At == CastStage.RingCounterspell
                   && !sc.HasHeldTouchCharge && string.IsNullOrEmpty(ring.RingCounterspellStored) && foe.Stats.CurrentHP == hpBefore
                   && !events.Contains("Resolve"),
                "Step 15: a Ring of Counterspells counters a held charge's delivery before the touch attack and the charge is gone (DMG p.230, PHB p.170-171; delegated RAW reading 2026-10-14)",
                ctx.Outcome + " held " + sc.HasHeldTouchCharge + " ring '" + ring.RingCounterspellStored + "' " + string.Join(",", events));
        }
        finally
        {
            sc.ClearHeldTouchCharge("CastCoreStageOrderTests");
            if (foe != null)
                Object.DestroyImmediate(foe.gameObject);
        }
    }

    /// <summary>
    /// Grease on an object a foe wears is an attack for Invisibility (PHB p.245 spares only actions directed at
    /// unattended objects; PHB p.237; delegated RAW reading 2026-10-14, batch 11 step 15): an invisible caster's Grease on
    /// the foe's armor ends its invisibility, on an ally's armor it does not.
    /// </summary>
    private static void TestGreaseInvisibility(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        CharacterController foe = null;
        try
        {
            foe = CreateEnemy("CastCoreGreaseFoe", caster.GridPosition + new Vector2Int(2, 0));
            foreach (bool onFoe in new[] { false, true })
            {
                CharacterController holder = onFoe ? foe : ally;
                caster.ApplyInvisibilityEffect(5, caster);
                CastContext ctx = Begin(gm, sc, caster, null, SpellNames.GREASE, new RecordingDecider(new List<string>()), out SpellSlot slot);
                ctx.Request.Choices.GreaseMode = GreaseCastMode.Armor;
                SpellCastRequest.ForUiGrease(ctx.Request, holder, null, null);
                gm.ExecuteCast(ctx, null);
                Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && caster.HasActiveInvisibilityEffect == !onFoe,
                    onFoe
                        ? "Step 15: an invisible caster's Grease on a foe's armor ends its invisibility (PHB p.245, p.237; delegated RAW reading 2026-10-14)"
                        : "Step 15: an invisible caster's Grease on an ally's armor keeps it invisible",
                    ctx.Outcome + " invisible " + caster.HasActiveInvisibilityEffect);
                StatusEffectManager effects = holder.StatusEffectManager;
                if (effects != null)
                    effects.ActiveEffects.RemoveAll(e => e != null && e.Spell != null && e.Spell.SpellId == "grease_armor");
                sc.SpellSlots.Remove(slot);
                caster.ForceEndInvisibility("CastCoreStageOrderTests");
            }
        }
        finally
        {
            caster.ForceEndInvisibility("CastCoreStageOrderTests");
            if (foe != null)
                Object.DestroyImmediate(foe.gameObject);
        }
    }

    /// <summary>
    /// The spell component pouch test on every PC path (batch 11 step 15; SPL-006, SPL-146; PHB p.174, p.130): an area
    /// Sleep, a Grease, a Summon Monster I and a hold of a touch spell given a material component are refused at Legality
    /// by a caster without a pouch, with nothing spent. Since batch 11 step 17 NPC casts make the same test
    /// (SpellComponentRulesTests).
    /// </summary>
    private static void TestPouchOnEveryPath(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        Assert(!SpellComponentRegistry.HasSpellComponentPouch(caster), "Step 15: the test wizard carries no spell component pouch");

        // Area: Sleep (M: sand, rose petals or a cricket, PHB p.280).
        CastContext area = BeginArea(gm, sc, caster, new RecordingDecider(new List<string>()), out SpellSlot slot);
        area.Request.Spell = SpellCastRequest.CloneForCast(SpellDatabase.GetSpell(SpellNames.SLEEP));
        slot.PreparedSpell = SpellDatabase.GetSpell(SpellNames.SLEEP).Clone();
        ExpectPouchRefusal(gm, area, slot, caster, "an area Sleep");
        sc.SpellSlots.Remove(slot);

        // Grease (M: a bit of pork rind or butter, PHB p.237).
        CastContext grease = Begin(gm, sc, caster, null, SpellNames.GREASE, new RecordingDecider(new List<string>()), out slot);
        grease.Request.Spell.HasMaterialComponent = true;
        grease.Request.Choices.GreaseMode = GreaseCastMode.Armor;
        SpellCastRequest.ForUiGrease(grease.Request, caster, null, null);
        ExpectPouchRefusal(gm, grease, slot, caster, "a Grease");
        sc.SpellSlots.Remove(slot);

        // Summon Monster I, given a material component on the request's own clone (refused before any placement).
        CastContext summon = Begin(gm, sc, caster, null, SpellNames.SUMMON_MONSTER_1, new RecordingDecider(new List<string>()), out slot);
        summon.Request.Spell.HasMaterialComponent = true;
        SpellCastRequest.ForUiSummon(summon.Request, caster.GridPosition + new Vector2Int(0, 2));
        ExpectPouchRefusal(gm, summon, slot, caster, "a Summon Monster I");
        sc.SpellSlots.Remove(slot);

        // A hold of Shocking Grasp, given a material component on the request's own clone.
        CastContext hold = Begin(gm, sc, caster, null, SpellNames.SHOCKING_GRASP, new RecordingDecider(new List<string>()), out slot);
        hold.Request.Spell.HasMaterialComponent = true;
        SpellCastRequest.ForUiHoldCharge(hold.Request);
        ExpectPouchRefusal(gm, hold, slot, caster, "a hold of a touch charge");
        Assert(!sc.HasHeldTouchCharge, "Step 15: the refused hold holds no charge");
        sc.SpellSlots.Remove(slot);

        // The delivery of a held charge is no casting (PHB p.141): its components were met at the hold, so a pouchless
        // caster delivers a charge whose spell has a material component.
        SpellData heldSpell = SpellCastRequest.CloneForCast(SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP));
        heldSpell.HasMaterialComponent = true;
        sc.SetHeldTouchCharge(heldSpell, null);
        caster.Actions.Reset();
        SpellCastRequest delivery = GameManager.BuildHeldChargeDeliveryRequest(caster, sc);
        delivery.Aim.Target = ally;
        CastOutcome deliveryLegality = gm.CheckLegality(delivery);
        Assert(delivery.Spell.HasMaterialComponent && deliveryLegality.Kind == CastOutcomeKind.Resolved,
            "Step 15: a held charge's delivery is not tested for the pouch again (its components were met at the hold, PHB p.141)", "" + deliveryLegality);
        sc.ClearHeldTouchCharge("CastCoreStageOrderTests");

        // M/DF (PHB p.174-175): a druid casts the divine version of Sleet Storm with its divine focus and needs no pouch;
        // the pouchless wizard's arcane version is refused (delegated RAW reading 2026-10-14).
        CharacterController druid = null;
        try
        {
            druid = TestHelpers.CreateCharacter("CastCoreDruid", "Druid", level: 7, wis: 18, gridPosition: caster.GridPosition + new Vector2Int(0, 4));
            druid.SetTeam(CharacterTeam.Player);
            SpellData sleet = SpellDatabase.GetSpell(SpellNames.SLEET_STORM);
            Assert(!SpellComponentRegistry.HasSpellComponentPouch(druid) && CastComponentRules.DivineFocusReplacesMaterial(druid, sleet)
                   && !CastComponentRules.DivineFocusReplacesMaterial(caster, sleet),
                "Step 15: a druid's Sleet Storm (M/DF) uses the divine focus, a wizard's the material component (PHB p.175)");

            SpellCastRequest druidCast = SpellCastRequest.ForUi(druid, sleet, null, new CastSource());
            SpellCastRequest.ForUiArea(druidCast, new List<CharacterController>(), new HashSet<Vector2Int> { druid.GridPosition + new Vector2Int(0, 1) });
            CastOutcome druidLegality = gm.CheckLegality(druidCast);
            caster.Actions.Reset();
            SpellCastRequest wizardCast = SpellCastRequest.ForUi(caster, sleet, null, new CastSource());
            SpellCastRequest.ForUiArea(wizardCast, new List<CharacterController>(), new HashSet<Vector2Int> { caster.GridPosition + new Vector2Int(0, 1) });
            CastOutcome wizardLegality = gm.CheckLegality(wizardCast);
            Assert(druidCast.Spell.HasMaterialComponent && druidLegality.Reason == SpellComponentRules.ReasonNoDivineFocus && wizardLegality.Reason == "no-component-pouch",
                "Step 15: a pouchless druid's area Sleet Storm is not refused for the pouch but, since step 17, for its divine focus (holly and mistletoe); a pouchless wizard's is refused for the pouch",
                druidLegality + " / " + wizardLegality);
        }
        finally
        {
            if (druid != null)
                Object.DestroyImmediate(druid.gameObject);
        }
    }

    private static void ExpectPouchRefusal(GameManager gm, CastContext ctx, SpellSlot slot, CharacterController caster, string label)
    {
        gm.ExecuteCast(ctx, null);
        Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Refused && ctx.Outcome.At == CastStage.Legality
               && ctx.Outcome.Reason == "no-component-pouch" && !slot.IsUsed && !caster.Actions.StandardActionUsed && ctx.Ended,
            "Step 15: " + label + " without a spell component pouch is refused at Legality with nothing spent (PHB p.174, p.130)", "" + ctx.Outcome);
    }

    /// <summary>
    /// Step-1 issue (f), SPL-146 (batch 11 step 15): a grappled caster cannot hold the charge of a somatic touch spell
    /// (PHB p.141: holding the charge is casting it; PHB p.156: no somatic component while grappling). The hold is refused
    /// at Legality with one log line and nothing spent; CheckLegality refuses the same request with no log line.
    /// </summary>
    private static void TestHoldWhileGrappled(GameManager gm, SpellcastingComponent sc, CharacterController caster)
    {
        var logs = new List<string>();
        Action<string> oldLog = ScenarioHooks.CombatLog;
        ScenarioHooks.CombatLog = m => logs.Add(m);
        caster.ApplyCondition(CombatConditionType.Grappled, 10, "UnitTest");
        try
        {
            CastContext ctx = Begin(gm, sc, caster, null, SpellNames.SHOCKING_GRASP, new RecordingDecider(new List<string>()), out SpellSlot slot);
            SpellCastRequest.ForUiHoldCharge(ctx.Request);

            CastOutcome pre = gm.CheckLegality(ctx.Request);
            Assert(pre.Kind == CastOutcomeKind.Refused && pre.Reason == "grapple" && logs.Count == 0,
                "Step 15: CheckLegality refuses a grappled caster's hold of a somatic touch spell and logs nothing (a pure pre-check)",
                pre + " / " + string.Join(" | ", logs));

            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Refused && ctx.Outcome.At == CastStage.Legality
                   && ctx.Outcome.Reason == "grapple" && !slot.IsUsed && !caster.Actions.StandardActionUsed && !sc.HasHeldTouchCharge,
                "Step 15: a grappled caster cannot hold a somatic touch spell: refused at Legality, nothing spent, no charge (PHB p.141, p.156; SPL-146)",
                "" + ctx.Outcome);
            Assert(logs.Count == 1 && logs[0].Contains("somatic"), "Step 15: the hold's grapple refusal logs one line", string.Join(" | ", logs));
            sc.SpellSlots.Remove(slot);
        }
        finally
        {
            ScenarioHooks.CombatLog = oldLog;
            caster.RemoveCondition(CombatConditionType.Grappled);
        }
    }

    /// <summary>
    /// A hold asks the caster's choices (batch 11 step 15, SPL-146) and keeps them with the charge: a held Resist Energy
    /// whose energy was chosen at the hold carries it (SpellcastingComponent.HeldTouchChoices), as a copy, so the
    /// delivery need not ask again (delegated RAW reading 2026-10-14).
    /// </summary>
    private static void TestHoldKeepsChoices(GameManager gm, SpellcastingComponent sc, CharacterController caster)
    {
        var events = new List<string>();
        CastContext ctx = Begin(gm, sc, caster, null, SpellNames.RESIST_ENERGY, new RecordingDecider(events), out SpellSlot slot);
        ctx.Request.Choices.Energy = ResistEnergyType.Cold;
        SpellCastRequest.ForUiHoldCharge(ctx.Request);
        gm.ExecuteCast(ctx, null);
        Assert(events.Contains("decider:fill") && ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && sc.HasHeldTouchCharge
               && sc.HeldTouchChoices != null && sc.HeldTouchChoices.Energy == ResistEnergyType.Cold
               && !ReferenceEquals(sc.HeldTouchChoices, ctx.Request.Choices),
            "Step 15: a hold asks the choices and the held charge keeps a copy of them (the energy type of a held Resist Energy)",
            ctx.Outcome + " " + string.Join(",", events));
        sc.ClearHeldTouchCharge("CastCoreStageOrderTests");
        Assert(sc.HeldTouchChoices == null, "Step 15: clearing the held charge clears its choices");
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// A hold whose choice dialog is open (the decider suspends at FillChoices) resumes once the energy is chosen: the
    /// dialog's answer goes to the open cast and GameManager.ResumeCastAfterChoiceDialog runs the hold again (batch 11 step
    /// 15). It finishes once with the charge held with that energy, and the delivery request built as the Discharge button
    /// builds it (GameManager.BuildHeldChargeDeliveryRequest) carries a copy of the energy, so the UI decider's choices do
    /// not suspend at the delivery.
    /// </summary>
    private static void TestHoldChoiceResume(GameManager gm, SpellcastingComponent sc, CharacterController caster)
    {
        MethodInfo resume = typeof(GameManager).GetMethod("ResumeCastAfterChoiceDialog", BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo subPhase = typeof(GameManager).GetProperty("CurrentSubPhase");
        Assert(resume != null && subPhase != null, "Step 15: GameManager.ResumeCastAfterChoiceDialog and CurrentSubPhase exist (reflection)");
        if (resume == null || subPhase == null)
            return;

        GameManager.PlayerSubPhase phaseBefore = gm.CurrentSubPhase;
        CastContext delivery = null;
        try
        {
            var events = new List<string>();
            var decider = new RecordingDecider(events);
            decider.Fills.Enqueue(CastChoiceFill.Suspended);
            CastContext ctx = Begin(gm, sc, caster, null, SpellNames.RESIST_ENERGY, decider, out SpellSlot slot);
            SpellCastRequest.ForUiHoldCharge(ctx.Request);
            gm.ExecuteCast(ctx, null);
            Assert(ctx.Outcome == null && decider.Finishes == 0 && !ctx.Ended && !sc.HasHeldTouchCharge && !slot.IsUsed,
                "Step 15: a hold suspends at its choice dialog with nothing spent and no charge", string.Join(",", events));

            ctx.Request.Choices.Energy = ResistEnergyType.Fire; // the dialog's answer (_pendingResistEnergyType writes the open cast)
            resume.Invoke(gm, new object[] { caster });
            Assert(decider.Finishes == 1 && ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && ctx.Ended && slot.IsUsed
                   && sc.HasHeldTouchCharge && sc.HeldTouchChoices != null && sc.HeldTouchChoices.Energy == ResistEnergyType.Fire,
                "Step 15: ResumeCastAfterChoiceDialog runs the hold again: one finish, the charge held with the chosen energy",
                ctx.Outcome + " finishes " + decider.Finishes + " " + string.Join(",", events));
            sc.SpellSlots.Remove(slot);

            SpellCastRequest req = GameManager.BuildHeldChargeDeliveryRequest(caster, sc);
            Assert(req != null && req.Source != null && req.Source.Kind == CastSourceKind.HeldChargeDelivery
                   && req.Choices.Energy == ResistEnergyType.Fire && !ReferenceEquals(req.Choices, sc.HeldTouchChoices),
                "Step 15: the delivery request starts from a copy of the hold's choices (the energy)");
            delivery = gm.BeginCast(req);
            Assert(gm.UiFillCastChoices(delivery) == CastChoiceFill.Filled,
                "Step 15: the UI decider's choices do not suspend at the delivery (the energy is already chosen)");
        }
        finally
        {
            if (delivery != null)
                gm.EndCast(delivery);
            sc.ClearHeldTouchCharge("CastCoreStageOrderTests");
            subPhase.SetValue(gm, phaseBefore);
        }
    }

    /// <summary>
    /// SPL-114 (batch 11 step 15): an AI caster's Silent Image (a Concentration-duration spell, PHB p.279) starts its
    /// concentration (BeginConcentrationTracking), and its next cast runs the casting-while-concentrating step in the
    /// ConcentrationOnCast stage, as a PC's does. Since batch 11 step 16 (decision D1, PHB p.176) that step ends the
    /// concentration with no roll: the stage's one trace mark is "ended", the caster no longer concentrates and the image
    /// is gone. The same for a PC's cast through the UI path (<see cref="TestPcCastEndsConcentration"/>).
    /// </summary>
    private static void TestAiConcentration(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        ConcentrationManager conc = caster.Concentration;
        bool added = conc == null;
        if (added)
        {
            conc = caster.gameObject.AddComponent<ConcentrationManager>();
            conc.Init(caster.Stats, caster);
        }
        if (caster.GetComponent<StatusEffectManager>() == null)
            caster.gameObject.AddComponent<StatusEffectManager>().Init(caster.Stats);
        Action<CharacterController, SpellData, string, string> oldStageHook = ScenarioHooks.CastStage;
        try
        {
            var events = new List<string>();
            SpellCastRequest image = BeginAi(sc, caster, caster, SpellNames.SILENT_IMAGE, events, out SpellSlot slot);
            bool cast = gm.RunCastSync(image, out CastOutcome o, stage => events.Add(stage.ToString()));
            Assert(cast && o.Kind == CastOutcomeKind.Resolved && conc.IsConcentrating && conc.ConcentrationSpellName == "Silent Image",
                "Step 15: an AI caster's Silent Image starts its concentration (SPL-114, PHB p.176)", o + " concentrating " + conc.IsConcentrating);
            sc.SpellSlots.Remove(slot);

            events = new List<string>();
            var marks = new List<string>();
            ScenarioHooks.CastStage = (c, s, stage, detail) =>
            {
                if (stage == CastStageTrace.ConcentrationOnCast)
                    marks.Add(detail);
            };
            SpellCastRequest missile = BeginAi(sc, caster, ally, SpellNames.RAY_OF_FROST, events, out slot);
            gm.RunCastSync(missile, out o, stage => events.Add(stage.ToString()));
            ScenarioHooks.CastStage = oldStageHook;
            Assert(events.Contains("ConcentrationOnCast") && marks.Count == 1 && marks[0] == "ended",
                "Step 16: the concentrating AI caster's next cast ends its concentration, with no check (decision D1, PHB p.176)",
                string.Join(",", marks) + " / " + string.Join(",", events));
            Assert(!conc.IsConcentrating && caster.StatusEffectManager != null && !caster.StatusEffectManager.HasEffect(SpellNames.SILENT_IMAGE),
                "Step 16: the AI caster's Silent Image ends when it casts Ray of Frost (no tail; PHB p.279)");
            sc.SpellSlots.Remove(slot);
        }
        finally
        {
            ScenarioHooks.CastStage = oldStageHook;
            if (conc != null && conc.IsConcentrating)
                conc.EndConcentration();
            if (added && conc != null)
                Object.DestroyImmediate(conc);
        }
    }

    /// <summary>
    /// Batch 11 step 16 (decision D1, PHB p.176): a PC concentrating on Minor Image (concentration + 2 rounds, PHB p.254)
    /// casts Ray of Frost through the UI path: the ConcentrationOnCast stage marks "ended" once, the PC no longer
    /// concentrates, and the image keeps going 2 more rounds at the caster's initiative count.
    /// </summary>
    private static void TestPcCastEndsConcentration(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally)
    {
        ConcentrationManager conc = caster.Concentration;
        bool added = conc == null;
        if (added)
        {
            conc = caster.gameObject.AddComponent<ConcentrationManager>();
            conc.Init(caster.Stats, caster);
        }
        if (caster.GetComponent<StatusEffectManager>() == null)
            caster.gameObject.AddComponent<StatusEffectManager>().Init(caster.Stats);
        Action<CharacterController, SpellData, string, string> oldStageHook = ScenarioHooks.CastStage;
        ActiveSpellEffect image = null;
        try
        {
            image = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.MINOR_IMAGE).Clone(), caster.Stats.CharacterName, 5);
            conc.BeginConcentration(image);
            var marks = new List<string>();
            ScenarioHooks.CastStage = (c, s, stage, detail) =>
            {
                if (stage == CastStageTrace.ConcentrationOnCast)
                    marks.Add(detail);
            };
            var events = new List<string>();
            var decider = new RecordingDecider(events);
            CastContext ctx = Begin(gm, sc, caster, ally, SpellNames.RAY_OF_FROST, decider, out SpellSlot slot);
            CastOutcome done = null;
            gm.ExecuteCast(ctx, o => done = o);
            ScenarioHooks.CastStage = oldStageHook;
            Assert(done != null && done.Kind == CastOutcomeKind.Resolved && marks.Count == 1 && marks[0] == "ended",
                "Step 16: a concentrating PC's cast ends its concentration once, with no check (decision D1, PHB p.176)",
                done + " / " + string.Join(",", marks));
            Assert(!conc.IsConcentrating && caster.StatusEffectManager.ActiveEffects.Contains(image) && image.RemainingRounds == 2 && image.DurationAnchor == caster,
                "Step 16: the PC's Minor Image runs its 2-round tail at the caster's count (PHB p.254, p.176)",
                "concentrating " + conc.IsConcentrating + " rounds " + image.RemainingRounds);
            sc.SpellSlots.Remove(slot);
        }
        finally
        {
            ScenarioHooks.CastStage = oldStageHook;
            if (conc != null && conc.IsConcentrating)
                conc.EndConcentration();
            if (image != null && caster.StatusEffectManager != null)
                caster.StatusEffectManager.RemoveEffect(image);
            if (added && conc != null)
                Object.DestroyImmediate(conc);
        }
    }

    /// <summary>
    /// Batch 11 step 16 review (decision D1, DMG p.213): a scroll whose spell needs no targeting (Shield) resolves outside
    /// the cast core (TryUseScroll's direct path, ResolveDirectItemCastChecks), and reading it still ends the reader's
    /// concentration with no roll: one "ended" mark, the Silent Image gone. A wand use there does not (SPL-114).
    /// </summary>
    private static void TestDirectScrollEndsConcentration(GameManager gm, CharacterController caster)
    {
        MethodInfo direct = typeof(GameManager).GetMethod("ResolveDirectItemCastChecks", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert(direct != null, "GameManager.ResolveDirectItemCastChecks exists");
        if (direct == null) return;
        ConcentrationManager conc = caster.Concentration;
        bool added = conc == null;
        if (added)
        {
            conc = caster.gameObject.AddComponent<ConcentrationManager>();
            conc.Init(caster.Stats, caster);
        }
        if (caster.GetComponent<StatusEffectManager>() == null)
            caster.gameObject.AddComponent<StatusEffectManager>().Init(caster.Stats);
        Action<CharacterController, SpellData, string, string> oldStageHook = ScenarioHooks.CastStage;
        ActiveSpellEffect image = null;
        try
        {
            SpellData shield = SpellDatabase.GetSpell(SpellNames.SHIELD);
#pragma warning disable CS0618
            ItemData scroll = ItemDatabase.CloneItem(ScrollFactory.GenerateScrollId(SpellNames.SHIELD, "Arcane"));
            ItemData wand = ItemDatabase.CloneItem(WandFactory.GenerateWandId(SpellNames.SHIELD));
#pragma warning restore CS0618
            Assert(scroll != null && wand != null, "The ItemDatabase has a scroll and a wand of Shield");
            if (scroll == null || wand == null) return;

            var marks = new List<string>();
            ScenarioHooks.CastStage = (c, s, stage, detail) =>
            {
                if (stage == CastStageTrace.ConcentrationOnCast)
                    marks.Add(detail);
            };
            image = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SILENT_IMAGE).Clone(), caster.Stats.CharacterName, 5);
            conc.BeginConcentration(image);
            object[] wandArgs = { caster, wand, shield, CastSourceKind.Wand, -1, null };
            direct.Invoke(gm, wandArgs);
            Assert(conc.IsConcentrating && marks.Count == 0,
                "Step 16: a wand of Shield used outside the cast core leaves the concentration (an open delegated question, SPL-114)",
                string.Join(",", marks));

            object[] args = { caster, scroll, shield, CastSourceKind.Scroll, -1, null };
            direct.Invoke(gm, args);
            Assert(!conc.IsConcentrating && marks.Count == 1 && marks[0] == "ended" && !caster.StatusEffectManager.ActiveEffects.Contains(image),
                "Step 16: reading a scroll of Shield outside the cast core ends the concentration on Silent Image, with no roll (decision D1, DMG p.213)",
                "concentrating " + conc.IsConcentrating + " / " + string.Join(",", marks));
        }
        catch (Exception ex)
        {
            Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
            Assert(false, "The direct scroll path ran without exceptions", inner.Message);
        }
        finally
        {
            ScenarioHooks.CastStage = oldStageHook;
            if (conc != null && conc.IsConcentrating)
                conc.EndConcentration();
            if (image != null && caster.StatusEffectManager != null && caster.StatusEffectManager.ActiveEffects.Contains(image))
                caster.StatusEffectManager.RemoveEffect(image);
            if (added && conc != null)
                Object.DestroyImmediate(conc);
        }
    }

    private static void TestMissingQuirk(GameManager gm, SpellcastingComponent sc, CharacterController caster, List<string> violations)
    {
        var events = new List<string>();
        var decider = new RecordingDecider(events);
        CastContext ctx = Begin(gm, sc, caster, null, SpellNames.SHOCKING_GRASP, decider, out SpellSlot slot);
        SpellCastRequest.ForUiHoldCharge(ctx.Request);
        // A hold ends with no combat-end check (EffectResolveSkipsCombatEndCheck, CORE-039), which its exit still requires
        // (batch 11 step 18 removed AlliesForgoEverySave, the bit this test used before; step 11 removed GlobeAfterCast).
        ctx.Request.Quirks = LegacyCastQuirkSets.UiHoldPath & ~LegacyCastQuirk.EffectResolveSkipsCombatEndCheck;
        int before = violations.Count;
        gm.ExecuteCast(ctx, null);

        Assert(violations.Count == before + 1 && violations[violations.Count - 1].Contains("EffectResolveSkipsCombatEndCheck"),
            "A request without a quirk a stage still needs raises one cast-core violation naming it", string.Join("; ", violations));
        Assert(ctx.Outcome != null && ctx.Outcome.Kind == CastOutcomeKind.Resolved && ctx.Outcome.At == CastStage.Resolve && sc.HasHeldTouchCharge,
            "...and gets the legacy behaviour anyway (the hold ends at Resolve with no combat-end check)", ctx.Outcome + " " + string.Join(",", events));
        if (sc.HasHeldTouchCharge)
            sc.ClearHeldTouchCharge("CastCoreStageOrderTests");
        sc.SpellSlots.Remove(slot);
    }

    /// <summary>
    /// Batch 11 step 18 (PHB p.177, SPL-149): the single-target path asks the target's own decider whether it gives up
    /// its save. The caster's Shield (a Buff) is given up; Daze (Will negates, a Debuff or Control spell after
    /// SpellCategoryClassifier.ReclassifyAll) on an ally is not, so the ally rolls its Will save; nothing requires a quirk.
    /// </summary>
    private static void TestVoluntarySave(GameManager gm, SpellcastingComponent sc, CharacterController caster, CharacterController ally,
        List<string> violations)
    {
        int before = violations.Count;
        CastContext shield = Begin(gm, sc, caster, caster, SpellNames.SHIELD, new RecordingDecider(new List<string>()), out SpellSlot slot);
        gm.ExecuteCast(shield, null);
        Assert(shield.Outcome != null && shield.Outcome.Kind == CastOutcomeKind.Resolved && shield.ForgoesSingleTargetSave,
            "Step 18: the caster gives up its save against its own Shield (a Buff, PHB p.177)", "" + shield.Outcome);
        sc.SpellSlots.Remove(slot);

        if (ally != null)
        {
            CastContext daze = Begin(gm, sc, caster, ally, SpellNames.DAZE, new RecordingDecider(new List<string>()), out slot);
            SpellEffectType dazeType = daze.Request.Spell.EffectType;
            gm.ExecuteCast(daze, null);
            Assert((dazeType == SpellEffectType.Debuff || dazeType == SpellEffectType.Control) && daze.Outcome != null && !daze.ForgoesSingleTargetSave,
                "Step 18: an ally does not give up its save against a Debuff or Control spell of its own side (PHB p.177; SPL-149)",
                dazeType + " " + daze.Outcome);
            sc.SpellSlots.Remove(slot);
        }
        Assert(violations.Count == before, "Step 18: a voluntary save requires no legacy quirk", string.Join("; ", violations));
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
