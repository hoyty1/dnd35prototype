using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The action catalog (batch 13 step 3, docs/architecture/combat-actions.md) as data, plus the rules of its executors:
/// - one entry per ActionIds constant, and every entry's cost, provocation, rules page, status and issues;
/// - a golden PHB p.141 Table 8-2 manifest (cost and provocation of every entry, the text extract's offset move-action
///   and full-round columns realigned: loading a hand or light crossbow, standing up, picking up, sheathing provoke;
///   drawing and readying a shield do not; a coup de grace provokes);
/// - every SpecialAttackType has one entry that agrees with ManeuverActionCost.For (cost, one melee attack);
/// - ActionCosts.From maps every CastActionCost; ActionCosts.CanAfford and SpentBetween on an action economy;
/// - Legality's bar (Refused(string) is Other; no reason text changes) and the ActionLegality adapters;
/// - ActionDeciders.For by source;
/// - a source scan: the executor files (Combat/Actions/Executors) never continue the UI (ShowActionChoices,
///   AfterAttackDelay, EndActivePCTurn, DelayedEndActivePCTurn) nor read IsControllable, ActivePC or IsPlayerTurn, and
///   ActionDeciders.cs is the only catalog file that reads IsControllable (its assertion count grows with the files);
/// - in Play mode only (a scene GameManager): the runner's refusal paths through GameManager.RunAIAction (a Missing
///   entry, no actor, a stand-up refused for a creature that is not prone, with nothing spent) and
///   ResolveActionProvocation's early exit when nobody threatens. The executors' live paths are first run by step 6's
///   scenarios.
/// The data part needs no GameManager, so the suite runs in either editor mode.
/// </summary>
public static class ActionCatalogTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== ACTION CATALOG TESTS (batch 13 step 3) ==========");

        Run(TestOneEntryPerId);
        Run(TestEveryEntryComplete);
        Run(TestStatusAndExecutors);
        Run(TestGoldenTable82);
        Run(TestManeuversAgreeWithCostTable);
        Run(TestCastCostMapping);
        Run(TestCostsOnActionEconomy);
        Run(TestLegalityBar);
        Run(TestLegalityAdapters);
        Run(TestDeciders);
        Run(TestLookups);
        Run(TestExecutorSourceScan);
        if (Application.isPlaying && GameManager.Instance != null)
            Run(TestRunnerRefusalsInPlay);
        else
            Debug.Log("  [SKIP] TestRunnerRefusalsInPlay (needs Play mode and a scene GameManager)");

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
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

    private static List<string> AllIdConstants()
    {
        var ids = new List<string>();
        foreach (FieldInfo f in typeof(ActionIds).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.IsLiteral && f.FieldType == typeof(string))
                ids.Add((string)f.GetRawConstantValue());
        return ids;
    }

    private static void TestOneEntryPerId()
    {
        List<string> ids = AllIdConstants();
        Assert(ids.Count >= 40, "ActionIds has the catalog's constants", ids.Count.ToString());
        Assert(new HashSet<string>(ids).Count == ids.Count, "Every ActionIds constant is distinct");

        var seen = new Dictionary<string, int>();
        foreach (ActionDefinition d in ActionCatalog.All)
            seen[d.Id] = seen.TryGetValue(d.Id, out int n) ? n + 1 : 1;

        var missing = new List<string>();
        var doubled = new List<string>();
        foreach (string id in ids)
        {
            if (!seen.TryGetValue(id, out int n))
                missing.Add(id);
            else if (n > 1)
                doubled.Add(id);
        }
        Assert(missing.Count == 0, "Every ActionIds constant has an entry", string.Join(", ", missing));
        Assert(doubled.Count == 0, "No ActionIds constant has two entries", string.Join(", ", doubled));

        var stray = new List<string>();
        var idSet = new HashSet<string>(ids);
        foreach (ActionDefinition d in ActionCatalog.All)
            if (!idSet.Contains(d.Id))
                stray.Add(d.Id);
        Assert(stray.Count == 0, "Every entry's id is an ActionIds constant", string.Join(", ", stray));
        Assert(ActionCatalog.All.Count == ids.Count, "The catalog has exactly one entry per constant", ActionCatalog.All.Count + " vs " + ids.Count);
    }

    private static void TestEveryEntryComplete()
    {
        var bad = new List<string>();
        foreach (ActionDefinition d in ActionCatalog.All)
        {
            if (string.IsNullOrWhiteSpace(d.Name)) bad.Add(d.Id + ": no name");
            if (!Enum.IsDefined(typeof(ActionCostType), d.Cost)) bad.Add(d.Id + ": cost");
            if (!Enum.IsDefined(typeof(ProvokeRule), d.Provokes)) bad.Add(d.Id + ": provocation");
            if (string.IsNullOrWhiteSpace(d.RulesRef)) bad.Add(d.Id + ": no RulesRef");
            else if (!(d.RulesRef.Contains("PHB") || d.RulesRef.Contains("DMG") || d.RulesRef.Contains("MM"))) bad.Add(d.Id + ": RulesRef names no book");
            if (d.IssueIds == null) bad.Add(d.Id + ": null IssueIds");
            if (d.UiAfter != ActionUiAfter.None && d.UiAfter != ActionUiAfter.ShowChoices && d.UiAfterSeconds <= 0f) bad.Add(d.Id + ": UiAfter without seconds");
        }
        Assert(bad.Count == 0, "Every entry has a name, a cost, a provocation and a RulesRef that names a book", string.Join("; ", bad));
    }

    private static void TestStatusAndExecutors()
    {
        var bad = new List<string>();
        foreach (ActionDefinition d in ActionCatalog.All)
        {
            bool built = d.Status == ImplStatus.Delegating || d.Status == ImplStatus.Extracted || d.Status == ImplStatus.New;
            if (built && d.Executor == null) bad.Add(d.Id + ": " + d.Status + " without an executor");
            if (!built && d.Executor != null) bad.Add(d.Id + ": " + d.Status + " with an executor");
            if (!built && (d.IssueIds == null || d.IssueIds.Length == 0)) bad.Add(d.Id + ": " + d.Status + " with no issue");
            if (d.IssueIds != null)
                foreach (string issue in d.IssueIds)
                    if (string.IsNullOrEmpty(issue) || !System.Text.RegularExpressions.Regex.IsMatch(issue, @"^[A-Z]+-\d{3}$"))
                        bad.Add(d.Id + ": issue id '" + issue + "'");
        }
        Assert(bad.Count == 0, "Built entries have an executor; unbuilt ones have none and name their issue", string.Join("; ", bad));
    }

    // id, cost, provocation, status, issues ("" for none). Table 8-2 (PHB p.141) realigned; see the class summary.
    private static readonly object[][] Golden =
    {
        new object[] { ActionIds.Attack, ActionCostType.Standard, ProvokeRule.Varies, ImplStatus.Delegating, "" },
        new object[] { ActionIds.FullAttack, ActionCostType.FullRound, ProvokeRule.Varies, ImplStatus.Delegating, "" },
        new object[] { ActionIds.FlurryOfBlows, ActionCostType.FullRound, ProvokeRule.Varies, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Charge, ActionCostType.FullRound, ProvokeRule.ByMovement, ImplStatus.Delegating, "" },
        new object[] { ActionIds.DeliverHeldCharge, ActionCostType.Standard, ProvokeRule.No, ImplStatus.Delegating, "" },
        new object[] { ActionIds.AttackWall, ActionCostType.Standard, ProvokeRule.Maybe, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.Trip, ActionCostType.Standard, ProvokeRule.Varies, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Disarm, ActionCostType.Standard, ProvokeRule.Yes, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Grapple, ActionCostType.Standard, ProvokeRule.Yes, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Sunder, ActionCostType.Standard, ProvokeRule.Yes, ImplStatus.Delegating, "" },
        new object[] { ActionIds.BullRush, ActionCostType.Standard, ProvokeRule.ByMovement, ImplStatus.Delegating, "" },
        new object[] { ActionIds.BullRushCharge, ActionCostType.FullRound, ProvokeRule.ByMovement, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Overrun, ActionCostType.Standard, ProvokeRule.ByMovement, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Feint, ActionCostType.Standard, ProvokeRule.No, ImplStatus.Delegating, "" },
        new object[] { ActionIds.CoupDeGrace, ActionCostType.FullRound, ProvokeRule.Yes, ImplStatus.Delegating, "" },
        new object[] { ActionIds.AidAnother, ActionCostType.Standard, ProvokeRule.Maybe, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.WakeAlly, ActionCostType.Standard, ProvokeRule.No, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.TurnUndead, ActionCostType.Standard, ProvokeRule.No, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.TotalDefense, ActionCostType.Standard, ProvokeRule.No, ImplStatus.New, "" },
        new object[] { ActionIds.CastSpell, ActionCostType.Standard, ProvokeRule.Yes, ImplStatus.Delegating, "" },
        new object[] { ActionIds.MaintainConcentration, ActionCostType.Standard, ProvokeRule.No, ImplStatus.Delegating, "" },
        new object[] { ActionIds.UseItem, ActionCostType.Standard, ProvokeRule.Varies, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.Move, ActionCostType.Move, ProvokeRule.ByMovement, ImplStatus.Delegating, "AI-076" },
        new object[] { ActionIds.Withdraw, ActionCostType.FullRound, ProvokeRule.ByMovement, ImplStatus.Delegating, "" },
        new object[] { ActionIds.FiveFootStep, ActionCostType.NotAnAction, ProvokeRule.No, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Run, ActionCostType.FullRound, ProvokeRule.Yes, ImplStatus.Missing, "CMB-202" },
        new object[] { ActionIds.Crawl, ActionCostType.Move, ProvokeRule.Yes, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.StandUp, ActionCostType.Move, ProvokeRule.Yes, ImplStatus.Delegating, "" },
        new object[] { ActionIds.DropProne, ActionCostType.Free, ProvokeRule.No, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.Smite, ActionCostType.Free, ProvokeRule.No, ImplStatus.Delegating, "" },
        new object[] { ActionIds.LayOnHands, ActionCostType.Standard, ProvokeRule.No, ImplStatus.Delegating, "" },
        new object[] { ActionIds.WildShape, ActionCostType.Standard, ProvokeRule.No, ImplStatus.Delegating, "" },
        new object[] { ActionIds.BardicMusic, ActionCostType.Standard, ProvokeRule.Varies, ImplStatus.Delegating, "" },
        new object[] { ActionIds.Rage, ActionCostType.Free, ProvokeRule.No, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.DomainPower, ActionCostType.Standard, ProvokeRule.No, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.Reload, ActionCostType.Move, ProvokeRule.Yes, ImplStatus.PcOnly, "AI-057" },
        new object[] { ActionIds.DrawWeapon, ActionCostType.Move, ProvokeRule.No, ImplStatus.Missing, "ITM-069" },
        new object[] { ActionIds.SheatheWeapon, ActionCostType.Move, ProvokeRule.Yes, ImplStatus.Missing, "ITM-069" },
        new object[] { ActionIds.ReadyShield, ActionCostType.Move, ProvokeRule.No, ImplStatus.Missing, "ITM-069" },
        new object[] { ActionIds.DonArmor, ActionCostType.FullRound, ProvokeRule.No, ImplStatus.Missing, "ITM-069" },
        new object[] { ActionIds.PickUpItem, ActionCostType.Move, ProvokeRule.Yes, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.DropItem, ActionCostType.Free, ProvokeRule.No, ImplStatus.PcOnly, "AI-054" },
        new object[] { ActionIds.BreathWeapon, ActionCostType.Standard, ProvokeRule.No, ImplStatus.AiOnly, "CRE-039,AI-040" },
        new object[] { ActionIds.Spittle, ActionCostType.Free, ProvokeRule.Varies, ImplStatus.AiOnly, "CRE-039" },
        new object[] { ActionIds.Engulf, ActionCostType.Standard, ProvokeRule.Varies, ImplStatus.AiOnly, "CRE-039" },
        new object[] { ActionIds.RangedSpecial, ActionCostType.Standard, ProvokeRule.Varies, ImplStatus.AiOnly, "CRE-039" },
        new object[] { ActionIds.Gaze, ActionCostType.Free, ProvokeRule.No, ImplStatus.OutOfScope, "CRE-076" },
        new object[] { ActionIds.Ready, ActionCostType.Standard, ProvokeRule.No, ImplStatus.Missing, "CMB-029" },
        new object[] { ActionIds.Delay, ActionCostType.NotAnAction, ProvokeRule.No, ImplStatus.Missing, "CMB-029" },
    };

    private static void TestGoldenTable82()
    {
        var covered = new HashSet<string>();
        var bad = new List<string>();
        foreach (object[] row in Golden)
        {
            string id = (string)row[0];
            covered.Add(id);
            ActionDefinition d = ActionCatalog.Get(id);
            if (d == null)
            {
                bad.Add(id + ": no entry");
                continue;
            }
            if (d.Cost != (ActionCostType)row[1]) bad.Add($"{id}: cost {d.Cost}, expected {row[1]}");
            if (d.Provokes != (ProvokeRule)row[2]) bad.Add($"{id}: provokes {d.Provokes}, expected {row[2]}");
            if (d.Status != (ImplStatus)row[3]) bad.Add($"{id}: status {d.Status}, expected {row[3]}");
            string issues = d.IssueIds != null ? string.Join(",", d.IssueIds) : "";
            if (issues != (string)row[4]) bad.Add($"{id}: issues '{issues}', expected '{row[4]}'");
        }
        Assert(bad.Count == 0, "Every entry matches the golden Table 8-2 manifest (cost, provocation, status, issues)", string.Join("; ", bad));

        var uncovered = new List<string>();
        foreach (ActionDefinition d in ActionCatalog.All)
            if (!covered.Contains(d.Id))
                uncovered.Add(d.Id);
        Assert(uncovered.Count == 0, "The golden manifest covers every entry", string.Join(", ", uncovered));
    }

    private static void TestManeuversAgreeWithCostTable()
    {
        var bad = new List<string>();
        foreach (SpecialAttackType type in (SpecialAttackType[])Enum.GetValues(typeof(SpecialAttackType)))
        {
            int count = 0;
            ActionDefinition entry = null;
            foreach (ActionDefinition d in ActionCatalog.All)
                if (d.Maneuver.HasValue && d.Maneuver.Value == type)
                {
                    count++;
                    entry = d;
                }
            if (count != 1)
            {
                bad.Add(type + ": " + count + " entries");
                continue;
            }
            ManeuverCost cost = ManeuverActionCost.For(type, null);
            if (entry.Cost != cost.Action) bad.Add($"{type}: catalog {entry.Cost}, ManeuverActionCost {cost.Action}");
            if (entry.SubstitutesForMeleeAttack != cost.ReplacesMeleeAttack) bad.Add($"{type}: substitute {entry.SubstitutesForMeleeAttack}, ManeuverActionCost {cost.ReplacesMeleeAttack}");
            if (entry.CostFor(null, null) != cost.Action) bad.Add($"{type}: CostFor(null) {entry.CostFor(null, null)}");
            if (ActionCatalog.ForManeuver(type) != entry) bad.Add(type + ": ForManeuver");
        }
        Assert(bad.Count == 0, "Every SpecialAttackType has one entry that agrees with ManeuverActionCost.For", string.Join("; ", bad));
    }

    private static void TestCastCostMapping()
    {
        var expected = new Dictionary<CastActionCost, ActionCostType>
        {
            { CastActionCost.ItemOrDelivery, ActionCostType.NotAnAction },
            { CastActionCost.Free, ActionCostType.Free },
            { CastActionCost.Swift, ActionCostType.Swift },
            { CastActionCost.Standard, ActionCostType.Standard },
            { CastActionCost.FullRound, ActionCostType.FullRound },
        };
        var bad = new List<string>();
        foreach (CastActionCost c in (CastActionCost[])Enum.GetValues(typeof(CastActionCost)))
        {
            if (!expected.TryGetValue(c, out ActionCostType want))
                bad.Add(c + ": not in the test's map");
            else if (ActionCosts.From(c) != want)
                bad.Add($"{c}: {ActionCosts.From(c)}, expected {want}");
        }
        Assert(bad.Count == 0, "ActionCosts.From maps every CastActionCost", string.Join("; ", bad));
    }

    private static void TestCostsOnActionEconomy()
    {
        var a = new ActionEconomy();
        Assert(ActionCosts.CanAfford(a, ActionCostType.FullRound) && ActionCosts.CanAfford(a, ActionCostType.Standard)
               && ActionCosts.CanAfford(a, ActionCostType.Move) && ActionCosts.CanAfford(a, ActionCostType.Swift)
               && ActionCosts.CanAfford(a, ActionCostType.Free), "A fresh turn can afford every action type");

        var before = new ActionCosts.Snapshot(a);
        a.UseMoveAction();
        Assert(ActionCosts.SpentBetween(before, new ActionCosts.Snapshot(a)) == ActionCostType.Move, "A move action spent reads as Move");
        Assert(!ActionCosts.CanAfford(a, ActionCostType.FullRound), "After a move no full-round action is left");
        Assert(ActionCosts.CanAfford(a, ActionCostType.Move), "After a move the standard action converts to a second move (PHB p.141)");

        var b = new ActionEconomy();
        var beforeB = new ActionCosts.Snapshot(b);
        b.UseStandardAction();
        b.UseMoveAction();
        Assert(ActionCosts.SpentBetween(beforeB, new ActionCosts.Snapshot(b)) == ActionCostType.FullRound, "A standard and a move together read as FullRound");

        var c = new ActionEconomy();
        var beforeC = new ActionCosts.Snapshot(c);
        Assert(ActionCosts.SpentBetween(beforeC, new ActionCosts.Snapshot(c)) == ActionCostType.Free, "Nothing spent reads as Free");
        c.UseSwiftAction();
        Assert(ActionCosts.SpentBetween(beforeC, new ActionCosts.Snapshot(c)) == ActionCostType.Swift, "A swift action spent reads as Swift");
        Assert(!ActionCosts.CanAfford(c, ActionCostType.Immediate), "An immediate action needs the unspent swift action");

        var single = new ActionEconomy { SingleActionOnly = true };
        var beforeSingle = new ActionCosts.Snapshot(single);
        single.UseStandardAction();
        Assert(ActionCosts.SpentBetween(beforeSingle, new ActionCosts.Snapshot(single)) == ActionCostType.Standard,
            "A disabled creature's standard action reads as Standard, not FullRound (PHB p.145)");
        var singleMove = new ActionEconomy { SingleActionOnly = true };
        var beforeSingleMove = new ActionCosts.Snapshot(singleMove);
        singleMove.UseMoveAction();
        Assert(ActionCosts.SpentBetween(beforeSingleMove, new ActionCosts.Snapshot(singleMove)) == ActionCostType.Move,
            "A disabled creature's move action reads as Move");

        var n = new ActionEconomy { MoveActionOnly = true };
        Assert(!ActionCosts.CanAfford(n, ActionCostType.Standard) && ActionCosts.CanAfford(n, ActionCostType.Move),
            "A nauseated turn affords a move action and no standard action (PHB p.310)");
        Assert(!ActionCosts.CanAfford((ActionEconomy)null, ActionCostType.Free), "No action economy affords nothing");
    }

    private static void TestLegalityBar()
    {
        Legality old = Legality.Refused("some reason");
        Assert(!old.Ok && old.Bar == LegalityBar.Other && old.Reason == "some reason", "Refused(string) maps to Other with its reason unchanged");
        Legality reach = Legality.Refused(LegalityBar.Reach, "out of reach");
        Assert(!reach.Ok && reach.Bar == LegalityBar.Reach && reach.Reason == "out of reach", "Refused(bar, reason) keeps both");
        Assert(Legality.Allowed().Ok && Legality.Allowed().Bar == LegalityBar.None, "Allowed has no bar");
        Assert(Legality.Refused(LegalityBar.None, "x").Bar == LegalityBar.Other, "A refusal never has the None bar");
        Assert(new Legality(true, "", LegalityRisk.WardSaveUnrolled).Risk == LegalityRisk.WardSaveUnrolled
               && new Legality(true, "", LegalityRisk.WardSaveUnrolled).Bar == LegalityBar.None, "The risk constructor is unchanged");
        Assert(old.ToString() == "refused(some reason)" && Legality.Allowed().ToString() == "ok", "ToString is unchanged");
    }

    private static void TestLegalityAdapters()
    {
        Assert(ActionLegality.FromDisabledReason("").Ok && ActionLegality.FromDisabledReason(null).Ok, "An empty disabled reason is allowed");
        Legality r = ActionLegality.FromDisabledReason("Prone creatures cannot charge", LegalityBar.Condition);
        Assert(!r.Ok && r.Reason == "Prone creatures cannot charge" && r.Bar == LegalityBar.Condition, "A disabled reason is refused with its text and bar");
        Assert(ActionLegality.FromCheck(true, "ignored").Ok, "FromCheck(true) is allowed");
        Assert(!ActionLegality.FromCheck(false, "no").Ok && ActionLegality.FromCheck(false, "no").Reason == "no", "FromCheck(false) keeps the reason");
        Legality range = ActionLegality.FromCast(new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "out-of-range"));
        Assert(!range.Ok && range.Bar == LegalityBar.Range && range.Reason == "out-of-range", "A cast refused out of range is a Range bar");
        Assert(ActionLegality.FromCast(new CastOutcome(CastOutcomeKind.Refused, CastStage.Legality, "invalid-target")).Bar == LegalityBar.Target,
            "A cast refused for its target is a Target bar");
        Assert(ActionLegality.FromCast(new CastOutcome(CastOutcomeKind.Resolved, CastStage.Legality, "ok")).Ok, "A legal cast is allowed");
        Assert(!ActionLegality.FromCast(null).Ok, "No cast outcome is refused");
    }

    private static void TestDeciders()
    {
        Assert(ActionDeciders.For(null, ActorSource.AI) is AiActionDecider, "The AI source gives the AI decider");
        Assert(ActionDeciders.For(null, ActorSource.Ui) is UiActionDecider, "The Ui source gives the UI decider");
        Assert(ActionDeciders.For(null) is AiActionDecider, "No actor and no source gives the AI decider");
        ProvocationAnswer? answer = null;
        AiActionDecider.Instance.ConfirmProvocation(null, null, AoOProvokingAction.DrinkPotion, "drink", null, a => answer = a);
        Assert(answer == ProvocationAnswer.Proceed && AiActionDecider.Instance.IsSynchronous, "The AI decider always goes on, at once");
        ProvocationAnswer? castAnswer = null;
        UiActionDecider.Instance.ConfirmProvocation(GameManager.Instance, null, AoOProvokingAction.CastSpell, "cast", null, a => castAnswer = a);
        Assert(castAnswer == (GameManager.Instance != null ? ProvocationAnswer.Unavailable : ProvocationAnswer.Proceed),
            "The UI decider shows no catalog prompt for a cast (the cast core has its own, with Cast Defensively)", castAnswer?.ToString());
    }

    private static void TestLookups()
    {
        Assert(ActionCatalog.Get("no-such-action") == null && ActionCatalog.Get(null) == null, "An unknown id has no entry");
        Assert(ActionCatalog.AvailableTo(null).Count == 0, "A missing actor has no actions");
        Assert(ActionCatalog.Get(ActionIds.Feint).CostFor(null, null) == ActionCostType.Standard, "A feint without Improved Feint is a standard action (PHB p.155)");
        Assert(ActionCatalog.Get(ActionIds.CastSpell).CostFor(null, null) == ActionCostType.Standard, "A cast with no spell given costs its default, a standard action");
        Assert(ActionCatalog.Get(ActionIds.BardicMusic).CostFor(null, new ActionContext { Variant = BardicMusicActionExecutor.StopVariant }) == ActionCostType.Free,
            "Stopping bardic music is a free action");
        Assert(ActionCatalog.Get(ActionIds.Attack).AiModeValuation == null, "The attack entries' AiModeValuation slot is empty until step 6");
        foreach (ActionDefinition d in ActionCatalog.All)
            if (d.Status == ImplStatus.OutOfScope)
                Assert(d.Availability == null, d.Id + " (out of scope) has no availability rule");
    }

    private static readonly string[] ForbiddenInExecutors =
    {
        "ShowActionChoices", "AfterAttackDelay", "EndActivePCTurn", "DelayedEndActivePCTurn", "IsControllable", "ActivePC", "IsPlayerTurn",
    };

    private static void TestExecutorSourceScan()
    {
        string dir = Path.Combine(Application.dataPath, "Scripts/Combat/Actions/Executors");
        if (!Directory.Exists(dir))
        {
            Assert(false, "Combat/Actions/Executors exists for the source scan", dir);
            return;
        }

        string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        Assert(files.Length >= 2, "The executor files are found", files.Length.ToString());
        foreach (string file in files)
        {
            string src = StripComments(file);
            foreach (string forbidden in ForbiddenInExecutors)
                Assert(!System.Text.RegularExpressions.Regex.IsMatch(src, @"\b" + forbidden + @"\b"),
                    Path.GetFileName(file) + " does not use " + forbidden);
        }

        string catalogDir = Path.Combine(Application.dataPath, "Scripts/Combat/Actions");
        foreach (string file in Directory.GetFiles(catalogDir, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "ActionDeciders.cs")
                continue;
            Assert(!StripComments(file).Contains("IsControllable"), Path.GetFileName(file) + " does not read IsControllable (only ActionDeciders.For does)");
        }
        Assert(StripComments(Path.Combine(catalogDir, "ActionDeciders.cs")).Contains("IsControllable"), "ActionDeciders.For reads IsControllable");
    }

    private static string StripComments(string path)
    {
        string text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        var code = new StringBuilder();
        foreach (string line in text.Split('\n'))
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            code.AppendLine(comment >= 0 ? line.Substring(0, comment) : line);
        }
        return code.ToString();
    }

    /// <summary>Runs <paramref name="routine"/> to its end in one go (only for paths that never wait); false if it waits.</summary>
    private static bool Drain(System.Collections.IEnumerator routine, int maxSteps = 1000)
    {
        var stack = new Stack<System.Collections.IEnumerator>();
        stack.Push(routine);
        for (int i = 0; i < maxSteps && stack.Count > 0; i++)
        {
            System.Collections.IEnumerator top = stack.Peek();
            if (!top.MoveNext())
            {
                stack.Pop();
                continue;
            }
            if (top.Current is System.Collections.IEnumerator nested)
                stack.Push(nested);
            else if (top.Current != null)
                return false;
        }
        return stack.Count == 0;
    }

    private static void TestRunnerRefusalsInPlay()
    {
        GameManager gm = GameManager.Instance;
        var go = new GameObject("ActionCatalogTest_Actor");
        try
        {
            var actor = go.AddComponent<CharacterController>();
            actor.Stats = new CharacterStats(
                name: "Catalog Tester", level: 1, characterClass: "Fighter",
                str: 12, dex: 12, con: 12, wis: 10, intelligence: 10, cha: 10,
                bab: 1, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 10, raceName: "Human");

            var missing = new ActionOutcome();
            bool drained = Drain(gm.RunAIAction(ActionContext.For(ActionIds.Run, actor, ActorSource.AI), missing));
            Assert(drained && !missing.Performed && missing.Refusal.Bar == LegalityBar.Other
                   && missing.Refusal.Reason.Contains("CMB-202") && missing.Spent == ActionCostType.NotAnAction,
                "RunAIAction refuses a Missing entry with its issue id, before any executor runs", missing.ToString());

            var noActor = new ActionOutcome();
            Drain(gm.RunAIAction(ActionContext.For(ActionIds.Attack, null, ActorSource.AI), noActor));
            Assert(!noActor.Performed && noActor.Refusal.Bar == LegalityBar.Target, "RunAIAction refuses a run with no actor", noActor.ToString());

            var standUp = new ActionOutcome();
            drained = Drain(gm.RunAIAction(ActionContext.For(ActionIds.StandUp, actor, ActorSource.AI), standUp));
            Assert(drained && !standUp.Performed && standUp.Refusal.Bar == LegalityBar.Condition && !standUp.RefusalLogged
                   && !actor.Actions.MoveActionUsed && standUp.Spent == ActionCostType.Free,
                "A stand-up for a creature that is not prone is refused unlogged and spends nothing (TryStandUpFromProneForAI's gate)",
                standUp.ToString());

            var provoke = new ActionOutcome();
            drained = Drain(gm.ResolveActionProvocation(actor, AoOProvokingAction.DrinkPotion, ActionDeciders.For(actor, ActorSource.Ui), provoke));
            Assert(drained && provoke.Refusal.Ok && !provoke.Cancelled && !provoke.Provoked,
                "ResolveActionProvocation asks nothing and provokes nothing when nobody threatens", provoke.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
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
