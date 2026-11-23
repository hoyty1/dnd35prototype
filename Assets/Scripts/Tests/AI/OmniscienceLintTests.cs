#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 3 (docs/designs/personalities_batch14.md "New facts"; design section 8, the omniscience lint): the
/// personality and valuation code reads other creatures only through the knowledge facade (ITargetView), so batch 17 can
/// swap in a provider that gates facts by perception and the considerations never notice. The lint fails when a file
/// under Assets/Scripts/AI/Personality or Assets/Scripts/AI/Valuation dereferences a target or a view's Subject into
/// .Stats, .GridPosition, .HasCondition or an .Is* member (a target is an identifier named subject, target*, *target,
/// foe*, enemy*, enemies, victim* or candidate*, or an element of one), and when it reads a view's .Subject at all,
/// except its .name (for a trace row) and a null comparison: an alias such as <c>var s = view.Subject;</c> is flagged
/// where it is made. The creature's own state goes through names that hold the observer (self, me, observer, npc,
/// actor) and view.Observer; a new identifier that holds a target must match the target pattern above (TargetName), or
/// the lint cannot see it. It skips SelfFacts.cs (the creature's own state) and TargetNumbers.cs (the one
/// [KnowledgeLegacy] choke point until batch 17), and ignores comments and strings. The matcher is checked on sample
/// lines first. Either mode (reads the source files).
/// </summary>
public static class OmniscienceLintTests
{
    private static int _passed;
    private static int _failed;

    private static readonly string[] Folders = { "Scripts/AI/Personality", "Scripts/AI/Valuation" };
    private static readonly HashSet<string> Skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SelfFacts.cs", "TargetNumbers.cs" };

    private const string Members = @"(?:Stats|GridPosition|HasCondition|Is[A-Z]\w*)\b";
    private static readonly Regex Dereference = new Regex(@"\b(?<id>[A-Za-z_]\w*)\s*(?:\[[^\]]*\])?\s*\??\.\s*" + Members, RegexOptions.Compiled);
    private static readonly Regex TargetName = new Regex(@"^(?:subject|target\w*|\w*target|foe\w*|enemy\w*|enemies|victim\w*|candidate\w*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>A read of a view's Subject other than its .name or a null comparison (the truth behind the facade).</summary>
    private static readonly Regex SubjectRead = new Regex(@"\??\.\s*Subject\b(?!\s*(?:\??\.\s*name\b|[!=]=\s*null\b))", RegexOptions.Compiled);

    private static readonly Regex Comments = new Regex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Strings = new Regex(@"@""(?:[^""]|"""")*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'", RegexOptions.Compiled);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== OMNISCIENCE LINT TESTS (batch 14 step 3) ======");

        TestMatcher();
        TestFolders();

        Debug.Log($"====== Omniscience Lint Results: {_passed} passed, {_failed} failed ======");
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
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    /// <summary>The forbidden reads in <paramref name="source"/>, as "line N: text".</summary>
    public static List<string> Violations(string source)
    {
        var found = new List<string>();
        if (string.IsNullOrEmpty(source))
            return found;
        // Blank comments and strings but keep the line breaks, so line numbers stay true.
        string code = Comments.Replace(source, m => Regex.Replace(m.Value, @"[^\n]", " "));
        code = Strings.Replace(code, m => new string(' ', m.Value.Length));
        string[] lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (Match m in Dereference.Matches(lines[i]))
                if (TargetName.IsMatch(m.Groups["id"].Value))
                    found.Add("line " + (i + 1) + ": " + m.Value.Trim());
            foreach (Match m in SubjectRead.Matches(lines[i]))
                found.Add("line " + (i + 1) + ": a Subject read (" + lines[i].Trim() + ")");
        }
        return found;
    }

    private static void TestMatcher()
    {
        var bad = new[]
        {
            "int hp = target.Stats.CurrentHP;",
            "bool d = view.Subject.IsDead;",
            "var p = foe?.GridPosition;",
            "if (enemies[i].HasCondition(CombatConditionType.Prone)) {}",
            "float ac = bestTarget.Stats.ArmorClass;",
            "bool c = candidate . IsUnconscious;",
            "var s = view.Subject; int hp = s.Stats.CurrentHP;",
            "CharacterController c = view.Subject;",
            "int team = (int)view.Subject.Team;",
            "var size = view?.Subject.GetCurrentSizeCategory();",
            "Foo(view.Subject);"
        };
        var good = new[]
        {
            "FactValue f = view.Get(FactKey.Down);",
            "int str = self.Stats.STRMod;",
            "CharacterController me = view.Observer; var s = me.Stats;",
            "// target.Stats.CurrentHP in a comment",
            "string s = \"target.Stats\";",
            "float ac = targetAC + 2; bool t = targeting.Equals(x);",
            "var v = KnowledgeService.ViewOf(self, target);",
            "string n = view != null && view.Subject != null ? view.Subject.name : \"?\";",
            "if (view.Subject == null) return 0f;",
            "string m = view?.Subject?.name;"
        };
        var missed = new List<string>();
        foreach (string s in bad)
            if (Violations(s).Count == 0)
                missed.Add(s);
        Assert(missed.Count == 0, "the lint flags a target's or Subject's .Stats, .GridPosition, .HasCondition and .Is*, and any other Subject read or alias",
            string.Join(" | ", missed));
        var wrong = new List<string>();
        foreach (string s in good)
            if (Violations(s).Count != 0)
                wrong.Add(s + " -> " + string.Join(", ", Violations(s)));
        Assert(wrong.Count == 0, "the lint passes facade reads, the creature's own stats, a Subject's name or null check, comments and strings",
            string.Join(" | ", wrong));
    }

    private static void TestFolders()
    {
        int files = 0;
        foreach (string folder in Folders)
        {
            string dir = Path.Combine(Application.dataPath, folder);
            if (!Directory.Exists(dir))
            {
                Assert(false, "the folder " + folder + " exists", dir);
                continue;
            }
            foreach (string path in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(path);
                if (Skipped.Contains(name))
                    continue;
                files++;
                List<string> v = Violations(File.ReadAllText(path));
                Assert(v.Count == 0, folder + "/" + name + " reads targets only through ITargetView", string.Join("; ", v));
            }
        }
        Assert(files >= 8, "the lint read the personality and valuation files", files + " files");
    }
}
}
#endif
