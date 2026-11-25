using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;
using SourceScan = Tests.Combat.CombatPacingTests;

namespace Tests.UI
{
/// <summary>
/// Grouped AI turns step 9 (gt-run-ahead; docs/designs/grouped_ai_turns.md "Run-ahead state kept hidden", "Input",
/// "Drains and clears"): what a held group keeps from the player. Checks:
/// while the presentation blocks input (a group holds or its beats still play) world input is refused
/// (GameManager.CanProcessWorldInput), End Turn does nothing (OnEndTurnButtonPressed) and the I, K and C toggles wait
/// (InputService.HandleKeyboardInput), each with the BlocksInput test first; IsPlayerTurn is not changed by a group;
/// a held view keeps its picture when its model changes and shows the change only when a Keyframe beat of a later
/// boundary applies; a view created inside a group stays hidden until its Spawned beat; a model destroyed inside a group
/// leaves its view drawn until its Removed beat; the source sites: the group open closes the sheet, skills and inventory
/// windows (C6), a prompt to a player-run creature closes the group and catches up first, the loot window opens through
/// WhenDrained (S4), and every reset clears the presentation.
/// Needs Play mode (Awake must run). The test objects sit off the grid and are destroyed afterwards; the presentation is
/// cleared at the end of every check.
/// </summary>
public static class PresentationHoldTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== PRESENTATION HOLD TESTS (grouped AI turns step 9) ==========");

        CombatPresentation.Clear();
        Run(TestSourceSites);
        if (!Application.isPlaying)
        {
            Assert(false, "The hold checks need Play mode (GameManager, live token views)");
        }
        else
        {
            Run(TestWorldInputRefused);
            Run(TestHeldViewKeepsItsPicture);
            Run(TestSpawnInsideGroupHidden);
            Run(TestRemovalInsideGroupWaits);
        }
        CombatPresentation.Clear();
        CombatPresentation.ResetGroupLog();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    // ── Source sites ───────────────────────────────────────────────────────────────────────────────────────────────

    private static void TestSourceSites()
    {
        string gm = Src("_Core/GameManager.cs");
        Assert(Regex.IsMatch(SourceScan.MethodBody(gm, "CanProcessWorldInput") ?? "", @"^\{\s*(//[^\n]*\n\s*)*if \(CombatPresentation\.BlocksInput\)\s*return false;"),
            "CanProcessWorldInput refuses first while the presentation blocks input (S1)");
        Assert(Regex.IsMatch(SourceScan.MethodBody(gm, "OnEndTurnButtonPressed") ?? "", @"^\{\s*(//[^\n]*\n\s*)*if \(CombatPresentation\.BlocksInput\) return;"),
            "OnEndTurnButtonPressed does nothing first while the presentation blocks input (S1)");
        Assert(Regex.IsMatch(SourceScan.MethodBody(Src("Services/InputService.cs"), "HandleKeyboardInput") ?? "",
                @"^\{\s*(//[^\n]*\n\s*)*if \(CombatPresentation\.BlocksInput\)\s*return;"),
            "The I, K and C toggles wait while the presentation blocks input (C6)");
        Assert(!(SourceScan.MethodBody(gm, "get_IsPlayerTurn") ?? "").Contains("CombatPresentation")
               && !Regex.IsMatch(gm, @"IsPlayerTurn\s*=>[^;]*CombatPresentation"),
            "IsPlayerTurn does not read the presentation (a group changes no rule)");
        string windows = SourceScan.MethodBody(gm, "ClosePlayerWindowsForGroup") ?? "";
        Assert(windows.Contains("CloseInventoryIfOpen()") && windows.Contains("SkillsUI.Close()")
               && (SourceScan.MethodBody(Src("Combat/Pacing/TurnControl.cs"), "AiTurnStarting") ?? "").Contains("ClosePlayerWindowsForGroup()"),
            "A group open closes the sheet, skills and inventory windows (C6)");
        string confirm = SourceScan.MethodBody(Src("UI/Combat/CombatUI.cs"), "ShowConfirmationDialog") ?? "";
        int prompt = confirm.IndexOf("TurnControl.PromptOpened()", StringComparison.Ordinal);
        int catchUp = confirm.IndexOf("CombatPresentation.CatchUpNow()", StringComparison.Ordinal);
        Assert(prompt >= 0 && catchUp > prompt, "A prompt while holding or behind closes the group, then catches the playback up before it shows");
        Assert(Src("_Core/GameManager.LootCollection.cs")
               .Contains("CombatPresentation.WhenDrained("), "The loot window opens when the playback drains (S4)");
        Assert((SourceScan.MethodBody(gm, "ResetCombatStateForNextEncounter") ?? "").Contains("CombatPresentation.Clear()"),
            "ResetCombatStateForNextEncounter clears the presentation");
        string harness = Src("_Core/GameManager.ScenarioHarness.cs");
        Assert((SourceScan.MethodBody(harness, "Harness_HaltCombat") ?? "").Contains("CombatPresentation.Clear()")
               && (SourceScan.MethodBody(harness, "Harness_ResetWorld") ?? "").Contains("CombatPresentation.Clear"),
            "The harness halt and world reset clear the presentation");
        string move = SourceScan.MethodBody(Src("Character/Controller/CharacterController.cs"), "MoveAlongPath") ?? "";
        int loop = move.IndexOf("for (int i = 0; i < path.Count; i++)", StringComparison.Ordinal);
        int stepRead = move.IndexOf("Mathf.Max(0.01f, CombatPacing.StepSeconds(secondsPerStep))", StringComparison.Ordinal);
        Assert(loop >= 0 && stepRead > loop, "MoveAlongPath reads the step time per step (a group closed mid-move returns the rest to live time)");
        string release = SourceScan.MethodBody(Src("UI/Presentation/TokenView.cs"), "Release") ?? "";
        int hold = release.IndexOf("Hold();", StringComparison.Ordinal);
        int finalKf = release.IndexOf("CombatPresentation.RecordFinalKeyframe(this)", StringComparison.Ordinal);
        int released = release.IndexOf("_released = true", StringComparison.Ordinal);
        Assert(hold >= 0 && finalKf > hold && released > finalKf,
            "TokenView.Release while queueing holds and records the last picture before it marks the view released");
    }

    // ── Play-mode checks ───────────────────────────────────────────────────────────────────────────────────────────

    private static readonly MethodInfo CanProcess =
        typeof(GameManager).GetMethod("CanProcessWorldInput", BindingFlags.Instance | BindingFlags.NonPublic);

    private static void TestWorldInputRefused()
    {
        GameManager gm = GameManager.Instance;
        Assert(gm != null && CanProcess != null, "GameManager and CanProcessWorldInput are found");
        if (gm == null || CanProcess == null)
            return;
        bool playerTurn = gm.IsPlayerTurn;
        var phase = gm.CurrentPhase;
        var sub = gm.CurrentSubPhase;
        CharacterController active = gm.ActivePC;
        try
        {
            CombatPresentation.OpenGroup();
            Assert(CombatPresentation.BlocksInput, "An open group blocks input");
            Assert(!(bool)CanProcess.Invoke(gm, null), "World input is refused while a group holds");
            Assert(gm.IsPlayerTurn == playerTurn, "IsPlayerTurn is unchanged by the group");
            gm.OnEndTurnButtonPressed();
            Assert(gm.CurrentPhase == phase && gm.CurrentSubPhase == sub && gm.ActivePC == active,
                "End Turn does nothing while a group holds");
            CombatPresentation.CloseGroup(GroupStop.Control);
            CombatPresentation.Enqueue(new PresentationBeat { Kind = BeatKind.Pace, Seconds = 1f, Pace = PaceKind.Ai });
            Assert(CombatPresentation.BlocksInput && !(bool)CanProcess.Invoke(gm, null),
                "World input is still refused while the closed group's beats play (behind)");
            CombatPresentation.CatchUpNow();
            Assert(!CombatPresentation.BlocksInput, "Drained: the presentation no longer blocks input");
        }
        finally
        {
            CombatPresentation.Clear();
        }
    }

    private static readonly MethodInfo ViewLateUpdate =
        typeof(TokenView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

    private static void RunLateUpdate(TokenView view)
    {
        if (view != null && ViewLateUpdate != null)
            ViewLateUpdate.Invoke(view, null);
    }

    private static CharacterController NewController(string name, Vector3 at)
    {
        var go = new GameObject(name);
        go.AddComponent<SpriteRenderer>();
        go.transform.position = at;
        return go.AddComponent<CharacterController>();
    }

    private static void DestroyToken(CharacterController cc, TokenView view)
    {
        if (cc != null)
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(cc);
            cc.ReleaseGrappleState("PresentationHoldTests cleanup");
            Object.DestroyImmediate(cc.gameObject);
        }
        if (view != null)
            Object.DestroyImmediate(view.gameObject);
    }

    private static void TestHeldViewKeepsItsPicture()
    {
        Vector3 start = new Vector3(-81f, -81f, 0f);
        Vector3 moved = new Vector3(-84f, -81f, 0f);
        CharacterController cc = NewController("PresentationHold_Held", start);
        TokenView view = cc.View;
        try
        {
            RunLateUpdate(view);
            Assert(view != null && (view.Body.position - start).sqrMagnitude < 1e-6f, "Control: the live view is at its model");
            if (view == null)
                return;
            CombatPresentation.OpenGroup();
            Assert(view.IsHeld, "A group open holds the view");
            cc.transform.position = moved;
            RunLateUpdate(view);
            Assert((view.Body.position - start).sqrMagnitude < 1e-6f, "A held view keeps its picture when its model moves (run-ahead state hidden)");
            CombatPresentation.Boundary(PresentationBoundary.Wait);
            RunLateUpdate(view);
            Assert((view.Body.position - start).sqrMagnitude < 1e-6f, "The boundary's keyframe is queued, not shown");
            CombatPresentation.CatchUpNow();
            Assert((view.Body.position - moved).sqrMagnitude < 1e-6f, "The keyframe shows the model's new position when its beat applies");
            CombatPresentation.CloseGroup(GroupStop.Control);
            Assert(!view.IsHeld, "The drained close releases the view to Live mode");
            cc.transform.position = start;
            RunLateUpdate(view);
            Assert((view.Body.position - start).sqrMagnitude < 1e-6f, "A released view follows its model again");
        }
        finally
        {
            CombatPresentation.Clear();
            DestroyToken(cc, view);
        }
    }

    private static void TestSpawnInsideGroupHidden()
    {
        CharacterController cc = null;
        TokenView view = null;
        try
        {
            CombatPresentation.OpenGroup();
            int before = CombatPresentation.PendingBeats;
            cc = NewController("PresentationHold_Spawn", new Vector3(-87f, -81f, 0f));
            view = cc.View;
            Assert(view != null && view.IsHeld && view.IsHiddenUntilSpawn, "A view created inside a group is held and hidden until its Spawned beat");
            Assert(view != null && view.Renderer != null && view.Renderer.forceRenderingOff, "Its renderer is not drawn yet");
            Assert(CombatPresentation.PendingBeats > before, "Its Spawned beat is queued");
            CombatPresentation.CatchUpNow();
            Assert(view != null && !view.IsHiddenUntilSpawn && view.Renderer != null && !view.Renderer.forceRenderingOff,
                "The Spawned beat shows the view");
        }
        finally
        {
            CombatPresentation.Clear();
            DestroyToken(cc, view);
        }
    }

    private static void TestRemovalInsideGroupWaits()
    {
        CharacterController cc = NewController("PresentationHold_Removed", new Vector3(-90f, -81f, 0f));
        TokenView view = cc.View;
        try
        {
            CombatPresentation.OpenGroup();
            int before = CombatPresentation.PendingBeats;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(cc);
            Object.DestroyImmediate(cc.gameObject);
            cc = null;
            Assert(view != null && view.IsReleased && view.gameObject != null, "A model destroyed inside a group leaves its view drawn (released from Live)");
            Assert(CombatPresentation.PendingBeats > before, "Its Removed beat is queued");
            Assert(view != null && view.IsHeld, "The released view is held until its Removed beat");
            Assert(view != null && view.Renderer != null && !view.Renderer.forceRenderingOff, "The view still shows its last picture before the Removed beat");
        }
        finally
        {
            CombatPresentation.Clear();
            DestroyToken(cc, view);
        }
    }

    // ── Plumbing ───────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Src(string relative)
    {
        string path = System.IO.Path.Combine(Application.dataPath, "Scripts", relative);
        return System.IO.File.Exists(path) ? SourceScan.StripComments(path) : "";
    }

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
