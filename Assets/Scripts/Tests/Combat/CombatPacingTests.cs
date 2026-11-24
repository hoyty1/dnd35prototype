using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The pacing service of grouped AI turns (step 1, owner request 2026-10-30; docs/designs/grouped_ai_turns.md "The pacing
/// service and the speed factor"): CombatPacing and the TurnControl marks.
/// - Live, speed 1: Wait returns a WaitForSeconds of exactly the caller's seconds, a new one per call, never an
///   IEnumerator, a CustomYieldInstruction or null (RunAIActionNow drains any IEnumerator in place), and its declared
///   return type is WaitForSeconds; StepSeconds returns the caller's value; Speed is 1;
/// - GameManager.InlineAction passes a CombatPacing.Wait leaf through unchanged (the same object, no extra frame);
/// - the marks set only the pacing kind (AiTurnStarting Ai; PlayerControlReturned and CombatEnded Player; PromptOpened
///   leaves it);
/// - a source scan of game code (Assets/Scripts without Tests/, comments and literals stripped): no
///   <c>new WaitForSeconds</c> (nor WaitForSecondsRealtime, nor a target-typed <c>new(</c>) outside CombatPacing.cs and
///   CombatPacing.ExemptWaitSites (each exempt method holds exactly its listed waits, and its file no other), every mark
///   sits at its named call site, and MoveAlongPath reads StepSeconds.
/// Pure and file reads only, so the suite runs in either editor mode.
/// </summary>
public static class CombatPacingTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== COMBAT PACING TESTS (grouped AI turns step 1) ==========");

        Run(TestLiveWait);
        Run(TestWaitIsLeaf);
        Run(TestStepSecondsAndSpeed);
        Run(TestInlineActionPassesPacingWait);
        Run(TestMarksSetKind);
        Run(TestNoRawWaitsInGameCode);
        Run(TestMarkSites);

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static readonly FieldInfo SecondsField =
        typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static void TestLiveWait()
    {
        Assert(SecondsField != null, "WaitForSeconds keeps its seconds in m_Seconds (read by reflection)");
        if (SecondsField == null)
            return;

        float variable = 0.8f; // a site's variable (ChargeCore's AoODelay, the crawl's aooWait)
        foreach (float s in new[] { 0f, 0.2f, 0.35f, 0.5f, 1.0f, 2.0f, variable })
        {
            WaitForSeconds w = CombatPacing.Wait(s);
            Assert(w != null && (float)SecondsField.GetValue(w) == s,
                $"Wait({s}) is a WaitForSeconds of exactly {s} s", w == null ? "null" : SecondsField.GetValue(w).ToString());
        }
        Assert(!ReferenceEquals(CombatPacing.Wait(0.5f), CombatPacing.Wait(0.5f)), "Live Wait builds a new WaitForSeconds per call, as the sites did");
    }

    private static void TestWaitIsLeaf()
    {
        MethodInfo m = typeof(CombatPacing).GetMethod(nameof(CombatPacing.Wait), BindingFlags.Public | BindingFlags.Static);
        Assert(m != null && m.ReturnType == typeof(WaitForSeconds), "CombatPacing.Wait is declared to return WaitForSeconds",
            m == null ? "missing" : m.ReturnType.Name);
        object w = CombatPacing.Wait(0.4f);
        Assert(w != null, "Wait never returns null");
        Assert(!(w is IEnumerator), "Wait never returns an IEnumerator (RunAIActionNow would drain it in place)");
        Assert(!(w is CustomYieldInstruction), "Wait never returns a CustomYieldInstruction (WaitUntil, WaitWhile)");
        Assert(w.GetType() == typeof(WaitForSeconds), "Wait returns exactly a WaitForSeconds", w.GetType().Name);
    }

    private static void TestStepSecondsAndSpeed()
    {
        Assert(CombatPacing.Speed == 1f, "Speed is fixed at 1 (step 6 reads GameSettings.aiTurnSpeed)", CombatPacing.Speed.ToString());
        foreach (float x in new[] { 0f, 0.005f, 0.12f, 0.2f, 1f })
            Assert(CombatPacing.StepSeconds(x) == x, $"StepSeconds({x}) is the caller's value", CombatPacing.StepSeconds(x).ToString());

        PaceKind saved = CombatPacing.Kind;
        try
        {
            foreach (PaceKind k in new[] { PaceKind.Player, PaceKind.Ai })
            {
                CombatPacing.Kind = k;
                Assert(CombatPacing.StepSeconds(0.2f) == 0.2f && (float)SecondsField.GetValue(CombatPacing.Wait(0.6f)) == 0.6f,
                    $"Speed 1: the {k} kind keeps every wait and step at the caller's seconds");
            }
        }
        finally
        {
            CombatPacing.Kind = saved;
        }
    }

    private static void TestInlineActionPassesPacingWait()
    {
        WaitForSeconds wait = CombatPacing.Wait(0.5f);
        IEnumerator Core() { yield return wait; }
        IEnumerator Executor() { yield return Core(); }

        var yielded = new List<object>();
        IEnumerator inline = GameManager.InlineAction(Executor(), 1);
        while (inline.MoveNext())
            yielded.Add(inline.Current);
        Assert(yielded.Count == 1 && ReferenceEquals(yielded[0], wait),
            "InlineAction passes a CombatPacing.Wait leaf through unchanged (the same object, one yield)", yielded.Count.ToString());
    }

    private static void TestMarksSetKind()
    {
        PaceKind saved = CombatPacing.Kind;
        try
        {
            CombatPacing.Kind = PaceKind.Player;
            TurnControl.AiTurnStarting(null);
            Assert(CombatPacing.Kind == PaceKind.Ai, "AiTurnStarting sets the Ai kind");
            TurnControl.PromptOpened();
            Assert(CombatPacing.Kind == PaceKind.Ai, "PromptOpened leaves the kind (the factor runs until control returns, C2)");
            TurnControl.PlayerControlReturned(null);
            Assert(CombatPacing.Kind == PaceKind.Player, "PlayerControlReturned sets the Player kind");
            TurnControl.AiTurnStarting(null);
            TurnControl.CombatEnded();
            Assert(CombatPacing.Kind == PaceKind.Player, "CombatEnded sets the Player kind");
        }
        finally
        {
            CombatPacing.Kind = saved;
        }
    }

    private static void TestNoRawWaitsInGameCode()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        string testsDir = Path.Combine(root, "Tests");
        var exempt = new Dictionary<string, (string Method, int Waits)>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in CombatPacing.ExemptWaitSites)
            exempt[Path.GetFullPath(Path.Combine(root, e.File))] = (e.Method, e.Waits);
        string pacingFile = Path.GetFullPath(Path.Combine(root, "Combat/Pacing/CombatPacing.cs"));

        int files = 0, routed = 0;
        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string full = Path.GetFullPath(file);
            if (full.StartsWith(Path.GetFullPath(testsDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;
            files++;
            string src = StripComments(full);
            routed += Regex.Matches(src, @"\bCombatPacing\.Wait\(").Count;
            int raw = CountRawWaits(src);
            if (string.Equals(full, pacingFile, StringComparison.OrdinalIgnoreCase))
            {
                Assert(raw == 1, "CombatPacing.cs builds the one WaitForSeconds of the service", raw.ToString());
                continue;
            }
            if (exempt.TryGetValue(full, out var site))
            {
                string body = MethodBody(src, site.Method);
                int inMethod = body == null ? -1 : CountRawWaits(body);
                Assert(inMethod == site.Waits && raw == site.Waits,
                    $"{Path.GetFileName(full)}: the {site.Waits} raw waits are all in the exempt {site.Method}",
                    $"in method {inMethod}, in file {raw}");
                continue;
            }
            if (raw > 0)
                offenders.Add($"{full.Substring(root.Length + 1)} ({raw})");
        }

        Assert(files > 100, "The scan reads the game code under Assets/Scripts", files.ToString());
        Assert(offenders.Count == 0, "No game code builds a raw WaitForSeconds outside CombatPacing.cs and the exempt sites",
            string.Join(", ", offenders));
        Assert(routed > 0, "Game code yields its pacing waits through CombatPacing.Wait", routed.ToString());
        Debug.Log($"  [INFO] {routed} pacing waits through CombatPacing.Wait in {files} game files");
    }

    private static void TestMarkSites()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        string gm = StripComments(Path.Combine(root, "_Core/GameManager.cs"));
        string turnStart = StripComments(Path.Combine(root, "_Core/GameManager.TurnStart.cs"));
        string combatEnd = StripComments(Path.Combine(root, "_Core/GameManager.CombatEnd.cs"));
        string cc = StripComments(Path.Combine(root, "Character/Controller/CharacterController.cs"));

        Assert(Regex.IsMatch(MethodBody(gm, "OnTurnStarted") ?? "",
                @"TurnControl\.AiTurnStarting\(character\);\s*StartCoroutine\(SingleNPCTurnFromInitiative\(character\)\);"),
            "OnTurnStarted marks AiTurnStarting right before the NPC turn starts");
        Assert(Regex.IsMatch(MethodBody(turnStart, "BeginForcedPCTurn") ?? "",
                @"TurnControl\.AiTurnStarting\(pc\);\s*StartCoroutine\(forcedTurn\);"),
            "BeginForcedPCTurn marks AiTurnStarting right before the forced turn starts");
        Assert(Regex.IsMatch(MethodBody(gm, "ShowActionChoices") ?? "",
                @"TurnControl\.PlayerControlReturned\(pc\);\s*CombatUI\.ShowActionButtonsForCharacter\(pc\);"),
            "ShowActionChoices marks PlayerControlReturned right before the action buttons show");
        foreach (string method in new[] { "HandleCombatVictoryDetected", "HandleCombatDefeatDetected" })
            Assert(Regex.IsMatch(MethodBody(combatEnd, method) ?? "", @"CurrentPhase = TurnPhase\.CombatOver;\s*TurnControl\.CombatEnded\(\);"),
                method + " marks CombatEnded right after the phase is set");
        foreach (string method in new[] { "OnCombatEnded", "ResetCombatStateForNextEncounter" })
            Assert((MethodBody(gm, method) ?? "").Contains("TurnControl.CombatEnded();"), method + " marks CombatEnded");
        Assert(Regex.IsMatch(MethodBody(cc, "MoveAlongPath") ?? "", @"Mathf\.Max\(0\.01f,\s*CombatPacing\.StepSeconds\(secondsPerStep\)\)"),
            "MoveAlongPath reads CombatPacing.StepSeconds, still clamped to 0.01 s");
    }

    /// <summary>
    /// The waits built outside the service, in every form a pacing wait could take: <c>new WaitForSeconds(</c> (also
    /// qualified as <c>UnityEngine.WaitForSeconds</c>), <c>new WaitForSecondsRealtime(</c>, and a C# 9 target-typed
    /// <c>WaitForSeconds w = new(</c>.
    /// </summary>
    private static int CountRawWaits(string code)
    {
        return Regex.Matches(code, @"\bnew\s+(?:UnityEngine\.)?WaitForSeconds(?:Realtime)?\s*\(").Count
            + Regex.Matches(code, @"\bWaitForSeconds(?:Realtime)?\s+\w+\s*=\s*new\s*\(").Count;
    }

    /// <summary>Words that can stand before a call but never as a declaration's return type.</summary>
    private static readonly HashSet<string> NotReturnTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "return", "new", "await", "else", "yield", "throw", "case", "in", "is", "as", "out", "ref", "goto", "using", "lock",
    };

    /// <summary>
    /// The body of the first method declared as <paramref name="name"/>: a return type (or modifier) word, the name, a
    /// parameter list and a body, with no parenthesis or <c>=</c> between the list and the brace, so a call such as
    /// <c>if (Name(x)) {</c> or <c>new Name() {</c> never matches.
    /// </summary>
    private static string MethodBody(string src, string name)
    {
        foreach (Match m in Regex.Matches(src, @"([\w<>\[\],.?]+)\s+" + Regex.Escape(name) + @"\s*\([^;{)]*\)[^;{=()]*\{"))
        {
            if (NotReturnTypes.Contains(m.Groups[1].Value))
                continue;
            int start = m.Index + m.Length - 1, depth = 0;
            for (int i = start; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}' && --depth == 0)
                    return src.Substring(start, i - start + 1);
            }
            return null;
        }
        return null;
    }

    /// <summary>
    /// The file's code with comments removed and the contents of string and char literals blanked (their quotes and line
    /// breaks kept), so a <c>//</c> or <c>/*</c> inside a literal (a URL in a log line) hides no code, and no wait, brace or
    /// call written inside a literal is counted.
    /// </summary>
    private static string StripComments(string path)
    {
        string s = File.ReadAllText(path);
        var code = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            char next = i + 1 < s.Length ? s[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < s.Length && s[i] != '\n')
                    i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                int stop = end < 0 ? s.Length : end + 2;
                for (; i < stop; i++)
                    if (s[i] == '\n')
                        code.Append('\n');
                code.Append(' ');
                continue;
            }
            if (c == '"' || c == '\'')
            {
                bool verbatim = c == '"' && i > 0 && (s[i - 1] == '@' || (s[i - 1] == '$' && i > 1 && s[i - 2] == '@'));
                code.Append(c);
                i++;
                while (i < s.Length)
                {
                    char d = s[i];
                    if (verbatim)
                    {
                        if (d == '"' && i + 1 < s.Length && s[i + 1] == '"') { code.Append("  "); i += 2; continue; }
                        if (d == '"') break;
                        code.Append(d == '\n' ? '\n' : ' ');
                        i++;
                        continue;
                    }
                    if (d == '\\') { code.Append("  "); i += 2; continue; }
                    if (d == c || d == '\n') break; // an unterminated literal ends at the line break
                    code.Append(' ');
                    i++;
                }
                if (i < s.Length)
                {
                    code.Append(s[i]);
                    i++;
                }
                continue;
            }
            code.Append(c);
            i++;
        }
        return code.ToString();
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
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " -- " + detail));
        }
    }
}
}
