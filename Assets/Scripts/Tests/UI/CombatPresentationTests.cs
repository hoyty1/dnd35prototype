using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;
using SourceScan = Tests.Combat.CombatPacingTests;

namespace Tests.UI
{
/// <summary>
/// Grouped AI turns step 8 (gt-presentation-gates, PURE; docs/designs/grouped_ai_turns.md "Recording (resolve now, show
/// later)", "`CombatUI` method classes"): the presentation facade in Passthrough only. Checks:
/// Passthrough is immediate (the flags; Defer and WhenDrained run at once; every CombatUI facade class, the SquareCell
/// highlight gate and the overlay clear apply before they return);
/// the queue mechanism step 9 builds on (an enqueued beat makes later facade calls queue behind it, CatchUpNow applies
/// them in rules order, then the drain actions);
/// CombatPresentation.Boundary never changes a returned yield, draws no die, and feeds only the presentation trace
/// (boundary lines, a token's keyframe only when it changed, state flushed at the boundary);
/// TestCombatUiMethodsClassified: every public CombatUI method is classed (reflection), and no class entry is stale;
/// the facade's shape in source (the log hook first, the gates, the hooks at their sites, the creature overloads);
/// the no-Random and no-rules-write scan of UI/Presentation/;
/// a trace that throws (a keyframe source) faults and stops itself, and never reaches the rules caller.
/// Step 9 (gt-run-ahead, Holding): with grouping off an AI turn's mark opens no group; an open group holds (a pacing wait
/// is a Pace beat and one cached one-frame yield, a facade call queues, WhenDrained waits), queue-while-behind keeps a
/// call after the close behind the group's beats, CatchUpNow applies in rules order and then drains, a close on an empty
/// queue drains at once, Timed playback holds the head for a Pace beat's seconds and Instant applies at once, and Clear
/// closes the group as a reset and drops the queue and the drain actions without showing them. The input refusals are
/// PresentationHoldTests.
/// Needs Play mode (a scene CombatUI and live token views).
/// Side effects on the shared Play pass: the facade probes add a few "[presentation test]" lines and a turn separator
/// to the scene's combat log (the log panel cannot trim back; no suite counts its lines). Every other probe restores
/// what it changed (the banner, the initiative bar, the action panel, the PC and NPC highlights, CombatPacing.Kind).
/// </summary>
public static class CombatPresentationTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== COMBAT PRESENTATION TESTS (grouped AI turns steps 8-9) ==========");

        PresentationTrace.Stop();
        CombatPresentation.Clear();

        Run(TestPassthroughFlags);
        Run(TestDeferAndWhenDrainedAtOnce);
        Run(TestBoundaryKeepsYieldAndDice);
        Run(TestCombatUiMethodsClassified);
        Run(TestFacadeSourceShape);
        Run(TestPresentationSourceScan);
        if (!Application.isPlaying)
        {
            Assert(false, "The facade checks need Play mode (a scene CombatUI and live token views)");
        }
        else
        {
            Run(TestSingletonOnItsOwnRoot);
            Run(TestFacadeAppliesAtOnce);
            Run(TestCellGateAppliesAtOnce);
            Run(TestQueueWhileBehindOrder);
            Run(TestTraceChannels);
            Run(TestTraceFaultIsolated);
            Run(TestGroupingOffOpensNoGroup);
            Run(TestHoldingQueueOrder);
            Run(TestCloseOnEmptyQueueDrains);
            Run(TestPlaybackTimedAndInstant);
            Run(TestClearDropsTheGroup);
            CombatPresentation.ResetGroupLog();
        }

        PresentationTrace.Stop();
        CombatPresentation.Clear();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    // ── Passthrough ────────────────────────────────────────────────────────────────────────────────────────────────

    private static void TestPassthroughFlags()
    {
        Assert(CombatPresentation.Mode == PresentationMode.Passthrough, "The mode is Passthrough (step 8)");
        Assert(!CombatPresentation.Holding, "Passthrough never holds");
        Assert(!CombatPresentation.Queueing, "Passthrough never queues");
        Assert(!CombatPresentation.BlocksInput, "Passthrough never blocks input");
        Assert(!CombatPresentation.HasPendingInteractive, "No interactive beat is pending");
        Assert(CombatPresentation.PendingBeats == 0 && CombatPresentation.PendingDrainActions == 0, "The queue and the drain list are empty");
    }

    private static void TestDeferAndWhenDrainedAtOnce()
    {
        int ran = 0;
        CombatPresentation.Defer(() => ran++, "probe");
        Assert(ran == 1, "Defer runs a one-shot visual before it returns in Passthrough");
        CombatPresentation.WhenDrained(() => ran++);
        Assert(ran == 2 && CombatPresentation.PendingDrainActions == 0, "WhenDrained runs at once on an empty queue");
        CombatPresentation.Defer(null, "null");
        CombatPresentation.WhenDrained(null);
        CombatPresentation.CatchUpNow();
        CombatPresentation.Clear();
        Assert(ran == 2 && !CombatPresentation.Queueing, "A null action, CatchUpNow and Clear are no-ops in Passthrough");
    }

    private static void TestBoundaryKeepsYieldAndDice()
    {
        Random.State before = Random.state;
        // The pacing kind is global: a player-paced wait is the caller's seconds whatever the AI speed; restored below.
        PaceKind kindBefore = CombatPacing.Kind;
        try
        {
            CombatPacing.Kind = PaceKind.Player;
            WaitForSeconds off = CombatPacing.Wait(0.75f);
            PresentationTrace.Begin();
            WaitForSeconds on = CombatPacing.Wait(0.75f);
            bool sameType = off != null && on != null && off.GetType() == typeof(WaitForSeconds) && on.GetType() == typeof(WaitForSeconds);
            Assert(sameType && SecondsOf(off) == 0.75f && SecondsOf(on) == 0.75f,
                "CombatPacing.Wait returns the same WaitForSeconds of the caller's seconds with the trace recording or not",
                SecondsOf(off) + " / " + SecondsOf(on));
            Assert(PresentationTrace.Lines.Any(l => l.Contains("\"ch\":\"b\"") && l.Contains("\"v\":\"wait\"")),
                "A recorded wait is a boundary line in the presentation trace");
            TurnControl.CombatEnded();
            Assert(PresentationTrace.Lines.Any(l => l.Contains("\"v\":\"combat-end\"")), "TurnControl.CombatEnded is a boundary");
            CombatPresentation.Boundary(PresentationBoundary.Control);
            CombatPresentation.RecordStep(null, new Vector2Int(1, 1), new Vector2Int(2, 1), Vector3.zero, Vector3.right, 0.2f);
            Assert(PresentationTrace.Lines.Any(l => l.Contains("\"ch\":\"step\"") && l.Contains("1,1>2,1")), "RecordStep traces the step");
        }
        finally
        {
            PresentationTrace.Stop();
            CombatPacing.Kind = kindBefore;
        }
        int lines = PresentationTrace.Lines.Count;
        CombatPresentation.Boundary(PresentationBoundary.Wait);
        Assert(PresentationTrace.Lines.Count == lines, "Outside a recording a boundary records nothing");
        Assert(SameState(before, Random.state), "Boundaries, keyframes and the trace draw no die (Random.state unchanged)");
    }

    private static float SecondsOf(WaitForSeconds w)
    {
        FieldInfo f = typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return w != null && f != null ? (float)f.GetValue(w) : -1f;
    }

    private static bool SameState(Random.State a, Random.State b) => a.Equals(b);

    // ── The CombatUI method classes ────────────────────────────────────────────────────────────────────────────────

    private static void TestCombatUiMethodsClassified()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        MethodInfo[] methods = typeof(CombatUI).GetMethods(flags).Where(m => !m.IsSpecialName).ToArray();
        List<string> names = methods.Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        List<string> unclassed = names.Where(n => CombatPresentation.ClassOf(n) == null).ToList();
        Assert(unclassed.Count == 0, "Every public CombatUI method is classed (" + names.Count + " names, " + methods.Length + " methods)",
            string.Join(", ", unclassed));
        List<string> stale = CombatPresentation.CombatUiClasses.Keys.Where(k => !names.Contains(k)).ToList();
        Assert(stale.Count == 0, "Every class entry names a public CombatUI method", string.Join(", ", stale));

        Assert(CombatPresentation.ClassOf("ShowCombatLog") == UiCallClass.Queued
               && CombatPresentation.ClassOf("UpdateAllStats4PC") == UiCallClass.Queued
               && CombatPresentation.ClassOf("SetActiveNPC") == UiCallClass.Queued,
            "The log, the stat panels and the highlights are Queued");
        Assert(CombatPresentation.ClassOf("ShowActionButtonsForCharacter") == UiCallClass.Control, "The action buttons show is the Control beat");
        Assert(CombatPresentation.ClassOf("ShowConfirmationDialog") == UiCallClass.Interactive
               && CombatPresentation.ClassOf("ShowDefeatPanel") == UiCallClass.Interactive,
            "The confirmation dialog and the defeat panel are Interactive barriers");
        Assert(CombatPresentation.ClassOf("ShowAoOConfirmationPrompt") == UiCallClass.CatchUp
               && CombatPresentation.ClassOf("ShowSpellSelection") == UiCallClass.CatchUp,
            "The other prompts and pickers are CatchUp");
        Assert(typeof(CombatUI).GetMethod("SetActivePC", new[] { typeof(int), typeof(CharacterController) }) != null
               && typeof(CombatUI).GetMethod("SetActiveNPC", new[] { typeof(int), typeof(CharacterController) }) != null,
            "SetActivePC and SetActiveNPC have their creature overloads");
        Debug.Log($"  [INFO] CombatUI public methods: {methods.Length} ({names.Count} names)");
    }

    // ── Source shape ───────────────────────────────────────────────────────────────────────────────────────────────

    private static string Src(string rel) => SourceScan.StripComments(Path.Combine(Application.dataPath, "Scripts", rel));

    private static void TestFacadeSourceShape()
    {
        string ui = Src("UI/Combat/CombatUI.cs");
        string log = SourceScan.MethodBody(ui, "ShowCombatLog") ?? "";
        Assert(Regex.IsMatch(log, @"^\{\s*ScenarioHooks\.CombatLog\?\.Invoke\(message\);"),
            "ScenarioHooks.CombatLog stays ShowCombatLog's first statement");

        foreach (string m in new[] { "ShowCombatLog", "AddTurnSeparator", "SetTurnIndicator", "UpdateInitiativeDisplay",
                     "SetActionButtonsVisible" })
            Assert((SourceScan.MethodBody(ui, m) ?? "").Contains("CombatPresentation.Queueing"), m + " is gated by the facade");
        foreach (string m in new[] { "UpdateAllStats", "UpdateAllStats4PC", "UpdateAllStatsMultiNPC" })
            Assert((SourceScan.MethodBody(ui, m) ?? "").Contains("PresentPanel("), m + " captures and presents a panel snapshot");
        Assert((SourceScan.MethodBody(ui, "PresentPanel") ?? "").Contains("CombatPresentation.Queueing"), "PresentPanel is gated by the facade");
        Assert(Regex.IsMatch(ui, @"void SetActivePC\(int pcNumber, CharacterController creature\)\s*\{\s*if \(CombatPresentation\.Queueing\)")
               && Regex.IsMatch(ui, @"void SetActiveNPC\(int npcIndex, CharacterController creature\)\s*\{\s*if \(CombatPresentation\.Queueing\)"),
            "The creature overloads of the highlights are gated by the facade");
        foreach (string m in new[] { "ShowActionButtonsForCharacter", "ShowConfirmationDialog", "ShowDefeatPanel" })
        {
            string body = SourceScan.MethodBody(ui, m) ?? "";
            Assert(Regex.IsMatch(body, @"^\{\s*CombatPresentation\.Boundary\(PresentationBoundary\.(Control|Interactive)\);")
                   && body.Contains("CombatPresentation.Queueing"),
                m + " is a boundary first, then the facade gate");
        }

        string cell = Src("Grid/SquareCell.cs");
        Assert(Regex.IsMatch(cell, @"void SetHighlight\(HighlightType type\)\s*\{[^}]*CombatPresentation\.Queueing")
               && Regex.IsMatch(cell, @"void SetHighlight\(Color color\)\s*\{[^}]*CombatPresentation\.Queueing")
               && Regex.IsMatch(cell, @"void ClearHighlight\(\)\s*\{[^}]*CombatPresentation\.Queueing"),
            "Both SquareCell.SetHighlight overloads and ClearHighlight are gated");

        string area = Src("Spell/AreaEffects/PersistentAreaEffect.cs");
        Assert((SourceScan.MethodBody(area, "CreateVisualIndicator") ?? "").Contains("CombatPresentation.AreaShown(this, VisualIndicator)")
               && (SourceScan.MethodBody(area, "OnDestroy") ?? "").Contains("CombatPresentation.AreaHidden(this, VisualIndicator)"),
            "PersistentAreaEffect's indicator creation and destruction call their hooks");
        Assert(Regex.IsMatch(SourceScan.MethodBody(area, "CreateVisualIndicator") ?? "", @"^\{\s*ReleaseIndicator\(\);")
               && Regex.IsMatch(SourceScan.MethodBody(area, "ExpireEffect") ?? "", @"ReleaseIndicator\(\);\s*Destroy\(gameObject\);")
               && (SourceScan.MethodBody(area, "ReleaseIndicator") ?? "").Contains("CombatPresentation.AreaHidden(this, indicator)"),
            "A re-creation and the expiry release the indicator in rules order, before the effect's deferred destruction");
        Assert((SourceScan.MethodBody(Src("Spell/AreaEffects/GustOfWindEffect.cs"), "SpawnWindLineVisual") ?? "").Contains("CombatPresentation.Defer("),
            "Gust of Wind's streaks go through CombatPresentation.Defer");

        Assert((SourceScan.MethodBody(Src("Combat/Pacing/CombatPacing.cs"), "Wait") ?? "").Contains("CombatPresentation.Boundary(PresentationBoundary.Wait);"),
            "CombatPacing.Wait is a boundary");
        Assert((SourceScan.MethodBody(Src("Combat/Pacing/TurnControl.cs"), "CombatEnded") ?? "").Contains("CombatPresentation.Boundary(PresentationBoundary.CombatEnd);"),
            "TurnControl.CombatEnded is a boundary");
        Assert(Regex.IsMatch(SourceScan.MethodBody(Src("Character/Controller/CharacterController.cs"), "MoveAlongPath") ?? "",
                @"GridPosition = nextCell\.Coords;\s*CombatPresentation\.RecordStep\(this, fromCell, nextCell\.Coords, startPos, endPos, secondsPerStep\);"),
            "Each MoveAlongPath step records its step right after GridPosition is set");

        string npcTurns = Src("_Core/GameManager.NPCTurns.cs");
        Assert(Regex.IsMatch(SourceScan.MethodBody(npcTurns, "SingleNPCTurnFromInitiative") ?? "", @"SetActiveNPC\(NPCs\.IndexOf\(npc\), npc\)"),
            "SingleNPCTurnFromInitiative highlights through the creature overload");
        Assert(Regex.IsMatch(SourceScan.MethodBody(Src("_Core/GameManager.cs"), "StartPCTurn") ?? "",
                @"SetActivePC\(pcIdx, pc\);[\s\S]*SetActiveNPC\(NPCs\.IndexOf\(pc\), pc\);"),
            "StartPCTurn highlights through the creature overloads");
    }

    /// <summary>The words UI/Presentation/ never uses: dice, rules writes, and SetActive or Destroy on a model.</summary>
    private static readonly (string Pattern, string Why)[] ForbiddenInPresentation =
    {
        (@"\bRandom\s*\.", "a UnityEngine.Random draw"),
        (@"\bSystem\.Random\b", "a System.Random"),
        (@"\bGridPosition\s*=[^=]", "a grid position write"),
        (@"\bCurrentHP\s*[-+*/]?=[^=]", "an HP write"),
        (@"\.(TakeDamage|ApplyIncomingDamage|DealDamage|ApplyCondition|RemoveCondition|AddCondition)\s*\(", "a damage or condition call"),
        (@"\bCurrentPhase\s*=[^=]|\bCurrentSubPhase\s*=[^=]", "a turn phase write"),
        (@"\b(AddToInitiative|RemoveFromInitiative|EndTurn|NextInitiativeTurn)\s*\(", "a turn or initiative call"),
        (@"\.Stats\.\w+\s*[-+]?=[^=]", "a stats write"),
        (@"\bNPCs\s*\.\s*(Add|Remove|RemoveAt|Insert|Clear)\s*\(", "an NPC list write"),
        (@"\bTryGetGrappleState\s*\(", "TryGetGrappleState (it ends a stale link; read PeekGrappleOpponent)"),
        (@"\b(_model|model|Model|_modelTransform)\s*\.\s*(gameObject\s*\.\s*)?SetActive\s*\(", "SetActive on a model"),
        (@"\bDestroy(Immediate)?\s*\(\s*(_model|model|Model|view\.Model|_modelRenderer|ModelRenderer)\b", "Destroy on a model"),
    };

    private static void TestPresentationSourceScan()
    {
        string dir = Path.Combine(Application.dataPath, "Scripts", "UI", "Presentation");
        string[] files = Directory.GetFiles(dir, "*.cs");
        Assert(files.Length >= 4, "UI/Presentation/ holds the presentation files", files.Length.ToString());
        var offenders = new List<string>();
        foreach (string f in files)
        {
            string code = SourceScan.StripComments(f);
            foreach ((string pattern, string why) in ForbiddenInPresentation)
                foreach (Match m in Regex.Matches(code, pattern))
                    offenders.Add(Path.GetFileName(f) + ": " + why + " (" + m.Value.Trim() + ")");
        }
        Assert(offenders.Count == 0, "UI/Presentation/ draws no die, writes no rules state and never sets a model active or destroys it",
            string.Join("; ", offenders));
    }

    // ── Play-mode checks ───────────────────────────────────────────────────────────────────────────────────────────

    private static void TestSingletonOnItsOwnRoot()
    {
        bool had = CombatPresentation.HasInstance;
        CombatPresentation p = CombatPresentation.Instance;
        Assert(p != null && p.transform.parent == null && p.gameObject.name == CombatPresentation.RootName,
            "The presentation singleton lives on its own root GameObject");
        Assert(p != null && p.GetComponent<GameManager>() == null && (GameManager.Instance == null || p.gameObject != GameManager.Instance.gameObject),
            "It is never on GameManager (CORE-009, CORE-013)");
        Assert(ReferenceEquals(CombatPresentation.Instance, p), "Instance returns the one singleton");
        if (!had && p != null)
            UnityEngine.Object.DestroyImmediate(p.gameObject);
    }

    private static CombatUI SceneUi()
    {
        GameManager gm = GameManager.Instance;
        return gm != null ? gm.CombatUI : null;
    }

    private static void TestFacadeAppliesAtOnce()
    {
        CombatUI ui = SceneUi();
        if (ui == null)
        {
            Assert(false, "The facade check needs the scene's CombatUI");
            return;
        }

        PresentationTrace.Begin();
        try
        {
            ui.ShowCombatLog("[presentation test] passthrough probe");
            string lastLog = PresentationTrace.Lines.LastOrDefault(l => l.Contains("\"ch\":\"log\"")) ?? "";
            Assert(lastLog.Contains("passthrough probe"), "ShowCombatLog applies its line before it returns");

            ui.AddTurnSeparator(77);
            Assert(PresentationTrace.Lines.Any(l => l.Contains("\"ch\":\"sep\"") && l.Contains("\"v\":\"77\"")), "AddTurnSeparator applies at once");

            if (ui.TurnIndicatorText != null)
            {
                string old = ui.TurnIndicatorText.text;
                ui.SetTurnIndicator("presentation probe");
                Assert(ui.TurnIndicatorText.text == "presentation probe", "SetTurnIndicator writes the banner before it returns");
                ui.SetTurnIndicator(old);
            }

            if (ui.InitiativeOrderText != null)
            {
                string old = ui.InitiativeOrderText.text;
                ui.UpdateInitiativeDisplay("presentation probe");
                Assert(ui.InitiativeOrderText.text == "presentation probe", "UpdateInitiativeDisplay writes the bar before it returns");
                ui.UpdateInitiativeDisplay(old);
            }

            if (ui.ActionPanel != null)
            {
                bool old = ui.ActionPanel.activeSelf;
                ui.SetActionButtonsVisible(!old);
                Assert(ui.ActionPanel.activeSelf == !old, "SetActionButtonsVisible applies before it returns");
                ui.SetActionButtonsVisible(old);
            }

            GameManager gm = GameManager.Instance;
            CharacterController pc = gm.PCs != null && gm.PCs.Count > 0 ? gm.PCs[0] : null;
            if (pc != null && pc.Stats != null && ui.PC1HPText != null)
            {
                ui.UpdateAllStats4PC(gm.PCs, gm.NPCs);
                Assert(ui.PC1HPText.text.Contains(pc.Stats.CurrentHP + "/"), "UpdateAllStats4PC writes the captured panel before it returns",
                    ui.PC1HPText.text);
            }

            int pcBefore = ActivePcNumber(ui);
            int npcBefore = ActiveNpcIndex(ui);
            try
            {
                ui.SetActivePC(0, null);
                ui.SetActiveNPC(-1, null);
                Assert(ActivePcNumber(ui) == 0 && ActiveNpcIndex(ui) == -1 && !CombatPresentation.Queueing,
                    "The highlight overloads apply at once and queue nothing");
            }
            finally
            {
                ui.SetActivePC(pcBefore);
                ui.SetActiveNPC(npcBefore);
            }
        }
        finally
        {
            PresentationTrace.Stop();
        }
    }

    /// <summary>The PC panel whose active indicator is on (1-4), or 0.</summary>
    private static int ActivePcNumber(CombatUI ui)
    {
        UnityEngine.UI.Image[] marks = { ui.PC1ActiveIndicator, ui.PC2ActiveIndicator, ui.PC3ActiveIndicator, ui.PC4ActiveIndicator };
        for (int i = 0; i < marks.Length; i++)
            if (marks[i] != null && marks[i].enabled)
                return i + 1;
        return 0;
    }

    /// <summary>The NPC panel whose active indicator is on (0-based), or -1.</summary>
    private static int ActiveNpcIndex(CombatUI ui)
    {
        if (ui.NPCPanels == null)
            return -1;
        for (int i = 0; i < ui.NPCPanels.Count; i++)
        {
            NPCPanelUI p = ui.NPCPanels[i];
            if (p != null && p.ActiveIndicator != null && p.ActiveIndicator.enabled)
                return i;
        }
        return -1;
    }

    private static void TestCellGateAppliesAtOnce()
    {
        var go = new GameObject("PresentationTestCell");
        try
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.1f, 0.2f, 0.3f, 1f);
            SquareCell cell = go.AddComponent<SquareCell>();
            cell.Init(-70, -70);
            cell.SetHighlight(HighlightType.Move);
            Assert(sr.color != new Color(0.1f, 0.2f, 0.3f, 1f), "SetHighlight(HighlightType) writes the renderer before it returns");
            Assert(cell.RequestedHighlight == HighlightType.Move, "The asked-for highlight is stored at once");
            cell.SetHighlight(HighlightType.None);
            Assert(sr.color == new Color(0.1f, 0.2f, 0.3f, 1f), "SetHighlight(None) restores the default colour at once");

            cell.SetHighlight(new Color(1f, 0f, 0f, 0.5f));
            SpriteRenderer overlay = go.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.gameObject.name == "CustomHighlightOverlay");
            Assert(overlay != null && overlay.enabled && overlay.color == new Color(1f, 0f, 0f, 0.5f), "SetHighlight(Color) shows the overlay at once");
            cell.ClearHighlight();
            Assert(overlay != null && !overlay.enabled && !cell.RequestedOverlayOn, "ClearHighlight hides the overlay at once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void TestQueueWhileBehindOrder()
    {
        var order = new List<string>();
        CombatUI ui = SceneUi();
        PresentationTrace.Begin();
        try
        {
            CombatPresentation.Enqueue(new PresentationBeat { Kind = BeatKind.Deferred, Text = "A", Show = () => order.Add("A") });
            Assert(CombatPresentation.Queueing && CombatPresentation.BlocksInput, "A queued beat makes the presentation queue (behind)");
            CombatPresentation.Defer(() => order.Add("B"), "B");
            CombatPresentation.WhenDrained(() => order.Add("drained"));
            if (ui != null)
            {
                ui.ShowCombatLog("[presentation test] queued probe");
                ui.AddCombatMessage("[presentation test] closure probe");
            }
            Assert(order.Count == 0 && CombatPresentation.PendingBeats == (ui != null ? 4 : 2) && CombatPresentation.PendingDrainActions == 1,
                "While behind, Defer and the facade queue and WhenDrained waits", "beats " + CombatPresentation.PendingBeats);
            Assert(!PresentationTrace.Lines.Any(l => l.Contains("queued probe")), "A queued log line is not applied yet");
            CombatPresentation.CatchUpNow();
            Assert(string.Join(",", order) == "A,B,drained", "CatchUpNow applies in rules order, then runs the drain actions", string.Join(",", order));
            if (ui != null)
            {
                Assert(PresentationTrace.Lines.Any(l => l.Contains("queued probe")), "CatchUpNow applies the queued log line through the same apply");
                List<string> defers = PresentationTrace.Lines.Where(l => l.Contains("\"ch\":\"defer\"")).ToList();
                Assert(PresentationTrace.Lines.Any(l => l.Contains("\"ch\":\"log\"") && l.Contains("closure probe"))
                       && defers.Count == 2 && defers[0].Contains("\"v\":\"A\"") && defers[1].Contains("\"v\":\"B\"")
                       && !defers.Any(l => l.Contains("\"v\":\"log\"")),
                    "A queued AddCombatMessage traces its log line only, as in Passthrough (no extra defer line); each Deferred beat writes its defer line",
                    string.Join(" | ", defers));
            }
            Assert(!CombatPresentation.Queueing, "The queue is empty again: Passthrough resumes");
        }
        finally
        {
            CombatPresentation.Clear();
            PresentationTrace.Stop();
        }
    }

    private static void TestTraceChannels()
    {
        CombatUI ui = SceneUi();
        PresentationTrace.Begin();
        try
        {
            CombatPresentation.Boundary(PresentationBoundary.Wait);
            int kf1 = PresentationTrace.Lines.Count(l => l.Contains("\"ch\":\"kf\""));
            int liveViews = TokenView.Live.Count(v => v != null && !v.IsReleased && !v.IsDespawning && v.Model != null);
            Assert(kf1 == liveViews, "The first boundary keyframes every live token view", kf1 + " of " + liveViews);
            CombatPresentation.Boundary(PresentationBoundary.Wait);
            int kf2 = PresentationTrace.Lines.Count(l => l.Contains("\"ch\":\"kf\""));
            Assert(kf2 == kf1, "An unchanged token gets no second keyframe", kf2 + " vs " + kf1);

            if (ui != null && ui.TurnIndicatorText != null)
            {
                string old = ui.TurnIndicatorText.text;
                ui.SetTurnIndicator("probe one");
                ui.SetTurnIndicator("probe two");
                CombatPresentation.Boundary(PresentationBoundary.Wait);
                List<string> states = PresentationTrace.Lines.Where(l => l.Contains("\"k\":\"indicator\"")).ToList();
                Assert(states.Count == 1 && states[0].Contains("probe two"), "State keys flush their last value at the boundary", string.Join(" | ", states));
                ui.SetTurnIndicator(old);
            }

            bool allJson = PresentationTrace.Lines.All(l => l.StartsWith("{\"n\":", StringComparison.Ordinal) && l.EndsWith("}", StringComparison.Ordinal));
            Assert(allJson, "Every trace line is one JSON object");
        }
        finally
        {
            PresentationTrace.Stop();
        }
    }

    /// <summary>
    /// The trace runs inside rules coroutines: a keyframe source that throws (a half-reset creature, say) must leave
    /// CombatPacing.Wait returning its normal yield; the trace faults, writes one error line and stops itself.
    /// </summary>
    private static void TestTraceFaultIsolated()
    {
        var model = new GameObject("PresentationFaultProbe");
        TokenView view = TokenView.Create(model, null);
        PaceKind kindBefore = CombatPacing.Kind;
        PresentationTrace.Begin();
        try
        {
            CombatPacing.Kind = PaceKind.Player;
            CombatPresentation.KeyframeSourceForTests = v => throw new InvalidOperationException("presentation test fault");
            WaitForSeconds w = null;
            string threw = null;
            try { w = CombatPacing.Wait(0.5f); }
            catch (Exception ex) { threw = ex.GetType().Name + ": " + ex.Message; }
            Assert(view != null && threw == null && w != null && SecondsOf(w) == 0.5f,
                "A throwing keyframe source never reaches CombatPacing.Wait's caller: it returns its normal yield", threw);
            Assert(!PresentationTrace.Recording && PresentationTrace.FaultMessage != null
                   && PresentationTrace.FaultMessage.Contains("presentation test fault"),
                "The fault stops the recording and is kept for the job's note", PresentationTrace.FaultMessage);
            Assert(PresentationTrace.Lines.Count(l => l.Contains("\"ch\":\"error\"")) == 1, "The fault writes one error line");
            threw = null;
            int lines = PresentationTrace.Lines.Count;
            try
            {
                CombatPacing.Wait(0.5f);
                CombatPresentation.RecordStep(null, new Vector2Int(1, 1), new Vector2Int(2, 1), Vector3.zero, Vector3.right, 0.2f);
            }
            catch (Exception ex) { threw = ex.GetType().Name + ": " + ex.Message; }
            Assert(threw == null && PresentationTrace.Lines.Count == lines, "After a fault the rest of the job runs untraced", threw);
            PresentationTrace.Begin();
            Assert(PresentationTrace.FaultMessage == null && PresentationTrace.Recording == PresentationTrace.Enabled,
                "The next job's Begin clears the fault");
        }
        finally
        {
            CombatPresentation.KeyframeSourceForTests = null;
            CombatPacing.Kind = kindBefore;
            PresentationTrace.Stop();
            if (view != null)
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            UnityEngine.Object.DestroyImmediate(model);
        }
    }

    // ── Holding (step 9) ───────────────────────────────────────────────────────────────────────────────────────────

    private static void TestGroupingOffOpensNoGroup()
    {
        PaceKind kind = CombatPacing.Kind;
        GameSettings settings = GameSettings.Instance;
        bool saved = settings.groupAiTurns;
        try
        {
            settings.groupAiTurns = false;
            TurnControl.AiTurnStarting(null);
            Assert(!CombatPresentation.Holding && !CombatPresentation.Queueing, "With grouping off an AI turn's mark opens no group (NEUTRAL default)");
            Assert(GameSettings.DefaultGroupAiTurns == false, "Grouping is off by default (step 9; Q1 in step 11)");
        }
        finally
        {
            settings.groupAiTurns = saved;
            if (kind == PaceKind.Player)
                TurnControl.PlayerControlReturned(null);
            CombatPresentation.Clear();
        }
    }

    private static void TestHoldingQueueOrder()
    {
        var order = new List<string>();
        try
        {
            Assert(CombatPresentation.OpenGroup(), "OpenGroup opens a group when none is open");
            Assert(!CombatPresentation.OpenGroup(), "OpenGroup refuses a second group while one is open");
            Assert(CombatPresentation.Holding && CombatPresentation.Mode == PresentationMode.Holding && CombatPresentation.Queueing
                   && CombatPresentation.BlocksInput, "An open group holds, queues and blocks input");
            GroupRecord g = CombatPresentation.CurrentGroup;
            Assert(g != null && g.IsOpen && CombatPresentation.GroupLog.Contains(g), "The open group is the current group and is in the group log");
            Assert(!TokenView.Live.Any(v => v != null && !v.IsReleased && !v.IsDespawning && v.Model != null && !v.IsHeld),
                "Every live token view is held while the group records");

            int before = CombatPresentation.PendingBeats;
            WaitForSeconds w1 = CombatPacing.Wait(0.75f);
            WaitForSeconds w2 = CombatPacing.Wait(2.5f);
            Assert(w1 != null && ReferenceEquals(w1, w2), "A pacing wait while holding yields one cached one-frame WaitForSeconds");
            Assert(CombatPresentation.PendingBeats >= before + 2, "Each pacing wait records a Pace beat", (CombatPresentation.PendingBeats - before).ToString());
            Assert(CombatPacing.StepSeconds(0.2f) == CombatPacing.HoldingStepSeconds, "A movement step while holding tweens over one frame (HoldingStepSeconds)",
                CombatPacing.StepSeconds(0.2f).ToString());

            CombatPresentation.Defer(() => order.Add("A"), "A");
            CombatPresentation.WhenDrained(() => order.Add("drained"));
            Assert(order.Count == 0 && CombatPresentation.PendingDrainActions == 1, "While holding, Defer queues and WhenDrained waits");
            int beats = g != null ? g.Beats : 0;
            CombatPresentation.CloseGroup(GroupStop.Control);
            Assert(!CombatPresentation.Holding && g != null && g.Stop == GroupStop.Control && g.CloseSeq >= g.OpenSeq && beats > 0,
                "CloseGroup ends the hold and records the stop, the seq span and the beats", "beats " + beats);
            Assert(CombatPresentation.Queueing && CombatPresentation.BlocksInput && order.Count == 0,
                "After the close the presentation is behind: still queueing, still blocking input, nothing shown");
            CombatPresentation.Defer(() => order.Add("B"), "B");
            Assert(order.Count == 0, "A call after the close queues behind the group's beats (queue while behind)");
            CombatPresentation.CatchUpNow();
            Assert(string.Join(",", order) == "A,B,drained", "CatchUpNow applies in rules order, then runs the drain actions", string.Join(",", order));
            Assert(!CombatPresentation.Queueing && !CombatPresentation.BlocksInput && !TokenView.AnyHeld,
                "Drained: Passthrough again and every view copies its model");
        }
        finally
        {
            CombatPresentation.Clear();
        }
    }

    private static void TestCloseOnEmptyQueueDrains()
    {
        int ran = 0;
        try
        {
            CombatPresentation.OpenGroup();
            CombatPresentation.WhenDrained(() => ran++);
            CombatPresentation.CatchUpNow();
            Assert(ran == 0 && CombatPresentation.Holding && CombatPresentation.PendingBeats == 0,
                "CatchUpNow while holding applies the beats but runs no drain action (the group still records)");
            CombatPresentation.CloseGroup(GroupStop.CombatEnd);
            Assert(ran == 1 && !CombatPresentation.Queueing, "A close on an empty queue drains at once (S4: the loot window opens now)");
            CombatPresentation.WhenDrained(() => ran++);
            Assert(ran == 2, "WhenDrained on an empty queue with no group open runs at once");
        }
        finally
        {
            CombatPresentation.Clear();
        }
    }

    private static void TestPlaybackTimedAndInstant()
    {
        PlaybackMode saved = CombatPresentation.Playback;
        var order = new List<string>();
        try
        {
            CombatPresentation.Playback = PlaybackMode.Timed;
            var probe = new PresentationBeat { Kind = BeatKind.Pace, Seconds = 1f, Pace = PaceKind.Player };
            float need = CombatPresentation.DurationOf(probe, PlaybackMode.Timed);
            CombatPresentation.Enqueue(new PresentationBeat { Kind = BeatKind.Pace, Seconds = 1f, Pace = PaceKind.Player });
            CombatPresentation.Enqueue(new PresentationBeat { Kind = BeatKind.Deferred, Text = "after", Show = () => order.Add("after") });
            CombatPresentation.Pump(need * 0.4f);
            Assert(need > 0f && order.Count == 0 && CombatPresentation.PendingBeats == 2, "Timed: a Pace beat holds the playback head for its seconds",
                "need " + need);
            CombatPresentation.Pump(need * 0.7f);
            Assert(order.Count == 1 && CombatPresentation.PendingBeats == 0, "Timed: past its seconds the head moves on and the next beat applies");
            Assert(CombatPresentation.DurationOf(probe, PlaybackMode.Instant) == 0f, "Instant: no beat holds the head");

            CombatPresentation.Playback = PlaybackMode.Instant;
            CombatPresentation.Enqueue(new PresentationBeat { Kind = BeatKind.Pace, Seconds = 5f, Pace = PaceKind.Ai });
            CombatPresentation.Enqueue(new PresentationBeat { Kind = BeatKind.Deferred, Text = "instant", Show = () => order.Add("instant") });
            CombatPresentation.Pump(0f);
            Assert(order.Count == 2 && CombatPresentation.PendingBeats == 0, "Instant: one frame applies every queued beat");
        }
        finally
        {
            CombatPresentation.Playback = saved;
            CombatPresentation.Clear();
        }
    }

    private static void TestClearDropsTheGroup()
    {
        int shown = 0;
        try
        {
            CombatPresentation.OpenGroup();
            GroupRecord g = CombatPresentation.CurrentGroup;
            CombatPresentation.Defer(() => shown++, "dropped");
            CombatPresentation.WhenDrained(() => shown++);
            CombatPresentation.Clear();
            Assert(!CombatPresentation.Holding && CombatPresentation.PendingBeats == 0 && CombatPresentation.PendingDrainActions == 0 && shown == 0,
                "Clear ends the hold and drops the queue and the drain actions without showing them");
            Assert(g != null && g.Stop == GroupStop.Reset, "Clear closes an open group as a reset");
            Assert(!TokenView.AnyHeld && !CombatPresentation.BlocksInput, "After Clear no view is held and input is free");
        }
        finally
        {
            CombatPresentation.Clear();
        }
    }

    // ── Plumbing ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static void Run(Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            Assert(false, $"{test.Method.Name} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail));
        }
    }
}
}
