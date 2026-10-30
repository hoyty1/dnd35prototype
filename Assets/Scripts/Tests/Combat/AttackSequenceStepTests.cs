using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The one attack-sequence executor (batch 12 step 17, docs/designs/pc_npc_parity_batch12.md; CMB-091, CMB-045, CMB-173,
/// D9): GameManager.RunAttackSequence runs every step through the same stages in the same order
/// (AttackSequenceStage: the decider's choice, CanCommitAttack, the legality gate, a ranged step's weapon checks, the ward's
/// commit, TryCommitAttack, a ranged step's AoOs, the roll, the post-attack hook), a full attack makes every step of the
/// ladder (Rapid Shot's extra one when declared before the first attack, PHB p.99), and the executor stops when the combat
/// ends (CombatOver) before or after a step. The requests are AI requests run synchronously (RunAttackSequenceSync), so no
/// prompt opens. Play only: CharacterController.Init needs Awake (TST-009) and the executor is a GameManager method.
/// Unseeded dice: the checks never depend on a hit (the targets are sturdy).
/// </summary>
public static class AttackSequenceStepTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== ATTACK SEQUENCE STEP TESTS (batch 12 step 17) ==========");

        GameManager gm = GameManager.Instance;
        if (gm == null || !Application.isPlaying)
        {
            Debug.Log("  [SKIP] the attack-sequence executor needs Play mode and the GameManager");
            Debug.Log($"========== ATTACK SEQUENCE STEP TESTS: {_passed} passed, {_failed} failed ==========");
            return;
        }

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        TestBuilders(gm);
        TestMeleeStepOrder(gm);
        TestRangedStepOrder(gm);
        TestFullAttackMakesEveryStep(gm);
        TestRapidShotDeclaredFirst(gm);
        TestStopsOnCombatEnd(gm);
        TestRapidShotNotDeclaredWithoutAShot(gm);
        TestFullAttackAfterPrepaidStep(gm);
        TestWardLostNaturalStepStaysUsed(gm);
        TestDefensiveRoundKeptAfterWardLostStep(gm);
        TestTwoWeaponStepsAndOffHandCap(gm);

        Debug.Log($"========== ATTACK SEQUENCE STEP TESTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string name, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {name}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {name}{(string.IsNullOrEmpty(detail) ? "" : " - " + detail)}");
        }
    }

    private static readonly AttackSequenceStage[] MeleeOrder =
    {
        AttackSequenceStage.ChooseNextStep, AttackSequenceStage.CanCommitAttack, AttackSequenceStage.CanDirectlyAttack,
        AttackSequenceStage.WardCommit, AttackSequenceStage.TryCommitAttack, AttackSequenceStage.ResolveStep,
        AttackSequenceStage.OnAttackResolved
    };

    private static readonly AttackSequenceStage[] RangedOrder =
    {
        AttackSequenceStage.ChooseNextStep, AttackSequenceStage.CanCommitAttack, AttackSequenceStage.CanDirectlyAttack,
        AttackSequenceStage.RangedWeaponChecks, AttackSequenceStage.WardCommit, AttackSequenceStage.TryCommitAttack,
        AttackSequenceStage.RangedAoO, AttackSequenceStage.ResolveStep, AttackSequenceStage.OnAttackResolved
    };

    private static string Join(IEnumerable<AttackSequenceStage> stages) => string.Join(",", stages);

    /// <summary>The builders: kind, origin, decider and step limit of each flow.</summary>
    private static void TestBuilders(GameManager gm)
    {
        CharacterController npc = null, target = null;
        try
        {
            npc = CreateCombatant("SeqBuild", 6, Vector2Int.zero, ItemID.WeaponLongsword);
            target = CreateCombatant("SeqBuildTarget", 4, Vector2Int.right, null);
            AttackSequenceRequest ai = AttackSequenceRequest.ForAI(gm, npc, target, null, null);
            Assert(ai.Kind == AttackSequenceKind.AI && ai.Origin == AttackOrigin.NpcMelee && ai.Decider is AiAttackDecider
                && ai.MaxSteps == int.MaxValue && ai.Intent.Mode == AttackMode.Melee,
                "ForAI: an AI full attack (no step limit), origin F, the AI decider, the NPC's own melee mode");
            AttackSequenceRequest aoo = AttackSequenceRequest.ForAoO(npc, target, npc.GetEquippedMainWeapon(), -4, null);
            Assert(aoo.Kind == AttackSequenceKind.AoO && aoo.Origin == AttackOrigin.AoO && aoo.MaxSteps == 1 && !aoo.CallerCanWait
                && aoo.Intent.IsAoO && aoo.AoOAttackModifier == -4 && aoo.Decider is AiAttackDecider,
                "ForAoO: one step, origin I, cannot wait, the AI decider, the Mobility modifier carried");
            AttackSequenceRequest ui = AttackSequenceRequest.ForUiAttack(gm, npc, target);
            Assert(ui.Kind == AttackSequenceKind.UiAttack && ui.Origin == AttackOrigin.PcSequence && ui.MaxSteps == 1,
                "ForUiAttack: one step per click, origin B");
            AttackSequenceRequest full = AttackSequenceRequest.ForUiFullAttack(gm, npc, target);
            Assert(full.Kind == AttackSequenceKind.UiFullAttack && full.Origin == AttackOrigin.PcFullAttackButton && full.MaxSteps == int.MaxValue,
                "ForUiFullAttack: every step, origin C");
        }
        finally
        {
            Cleanup(npc, target);
        }
    }

    /// <summary>One melee step: the seven stages a melee step runs, in order; the step is paid and recorded.</summary>
    private static void TestMeleeStepOrder(GameManager gm)
    {
        CharacterController npc = null, target = null;
        try
        {
            npc = CreateCombatant("SeqMelee", 6, Vector2Int.zero, ItemID.WeaponLongsword);
            target = CreateCombatant("SeqMeleeTarget", 4, Vector2Int.right, null);
            AttackSequenceRequest req = AttackSequenceRequest.ForAI(gm, npc, target, null, null);
            req.MaxSteps = 1;
            req.CallerCanWait = false;
            var o = new AttackSequenceOutcome();
            gm.RunAttackSequenceSync(req, o);
            Assert(Join(o.Stages) == Join(MeleeOrder), "A melee step runs choose, can-commit, legality, ward, commit, roll, hook", Join(o.Stages));
            Assert(o.AttacksMade == 1 && npc.ProgressiveAttackPool.MainHandStepsUsed == 1 && !npc.Actions.HasStandardAction && npc.Actions.HasMoveAction,
                "The one step is recorded and paid with the standard action (PHB p.143)",
                $"attacks {o.AttacksMade}, steps {npc.ProgressiveAttackPool.MainHandStepsUsed}");
        }
        finally
        {
            Cleanup(npc, target);
        }
    }

    /// <summary>One ranged step: the ranged weapon checks before the ward, the AoO stage after the commit.</summary>
    private static void TestRangedStepOrder(GameManager gm)
    {
        CharacterController archer = null, target = null;
        try
        {
            archer = CreateCombatant("SeqRanged", 6, Vector2Int.zero, ItemID.WeaponLongbow);
            GiveArrows(archer, 10);
            target = CreateCombatant("SeqRangedTarget", 4, new Vector2Int(6, 0), null);
            AttackSequenceRequest req = AttackSequenceRequest.ForAI(gm, archer, target, null, null);
            req.MaxSteps = 1;
            req.CallerCanWait = false;
            var o = new AttackSequenceOutcome();
            gm.RunAttackSequenceSync(req, o);
            Assert(req.Intent.Mode == AttackMode.Ranged, "The NPC archer's intent is Ranged (its own weapon, CMB-096)");
            Assert(Join(o.Stages) == Join(RangedOrder),
                "A ranged step runs choose, can-commit, legality, weapon checks, ward, commit, AoO, roll, hook", Join(o.Stages));
            Assert(o.AttacksMade == 1 && ArrowCount(archer) == 9, "One shot, one arrow spent (PHB p.113)", $"arrows {ArrowCount(archer)}");

            // No ammunition: the weapon checks stop the step before the ward, the commit and the AoOs (nothing spent).
            CharacterController empty = CreateCombatant("SeqRangedEmpty", 6, new Vector2Int(0, 3), ItemID.WeaponLongbow);
            try
            {
                AttackSequenceRequest dry = AttackSequenceRequest.ForAI(gm, empty, target, null, null);
                dry.CallerCanWait = false;
                var d = new AttackSequenceOutcome();
                gm.RunAttackSequenceSync(dry, d);
                Assert(d.AttacksMade == 0 && d.Stages.Count == 4 && d.Stages[3] == AttackSequenceStage.RangedWeaponChecks
                    && empty.Actions.HasStandardAction && empty.ProgressiveAttackPool.MainHandStepsUsed == 0,
                    "An empty quiver stops at the weapon checks: no ward save, no action, no step", Join(d.Stages) + " stop " + d.StopReason);
            }
            finally
            {
                Cleanup(empty);
            }
        }
        finally
        {
            Cleanup(archer, target);
        }
    }

    /// <summary>A BAB +6 full attack makes both iteratives at +6 and +1, the second paying the move action (PHB p.143).</summary>
    private static void TestFullAttackMakesEveryStep(GameManager gm)
    {
        CharacterController npc = null, target = null;
        try
        {
            npc = CreateCombatant("SeqFull", 6, Vector2Int.zero, ItemID.WeaponLongsword);
            target = CreateCombatant("SeqFullTarget", 4, Vector2Int.right, null);
            AttackSequenceRequest req = AttackSequenceRequest.ForAI(gm, npc, target, null, null);
            req.CallerCanWait = false;
            var o = new AttackSequenceOutcome();
            gm.RunAttackSequenceSync(req, o);
            int hooks = o.Stages.FindAll(s => s == AttackSequenceStage.OnAttackResolved).Count;
            Assert(o.AttacksMade == 2 && hooks == 2 && o.Aggregate.Attacks.Count == 2
                && o.Aggregate.Attacks[0].BreakdownBAB == 6 && o.Aggregate.Attacks[1].BreakdownBAB == 1,
                "A BAB +6 full attack: two steps at +6 and +1, each through the hook",
                $"attacks {o.AttacksMade}, hooks {hooks}");
            Assert(npc.ProgressiveAttackPool.IsFullAttack && !npc.Actions.HasMoveAction && o.StopReason != null,
                "The second step made it a full attack (move action spent); the ladder's end stopped it", o.StopReason);
        }
        finally
        {
            Cleanup(npc, target);
        }
    }

    /// <summary>
    /// D9: an archer whose Rapid Shot toggle is on declares it at its first ranged step, before the first attack: three
    /// shots at BAB +6 (+6, +6, +1), each with -2, the full-round action spent by the declaration (PHB p.99).
    /// </summary>
    private static void TestRapidShotDeclaredFirst(GameManager gm)
    {
        CharacterController archer = null, target = null;
        try
        {
            archer = CreateCombatant("SeqRapid", 6, Vector2Int.zero, ItemID.WeaponLongbow, "Rapid Shot");
            GiveArrows(archer, 10);
            target = CreateCombatant("SeqRapidTarget", 4, new Vector2Int(6, 0), null);
            RangeInfo range = RangeCalculator.GetRangeInfo(6, 100, false);
            int singleMod = AttackMod(archer.Attack(target, false, 0, null, range, 6, archer.GetEquippedMainWeapon()));
            archer.SetRapidShot(true);
            AttackSequenceRequest req = AttackSequenceRequest.ForAI(gm, archer, target, null, null);
            req.CallerCanWait = false;
            var o = new AttackSequenceOutcome();
            gm.RunAttackSequenceSync(req, o);
            Assert(o.RapidShotDeclared && o.AttacksMade == 3 && ArrowCount(archer) == 7,
                "With the toggle on, Rapid Shot is declared before the first shot: three shots, three arrows",
                $"declared {o.RapidShotDeclared}, attacks {o.AttacksMade}, arrows {ArrowCount(archer)}");
            if (o.Aggregate.Attacks.Count == 3)
            {
                Assert(o.Aggregate.Attacks[0].BreakdownBAB == 6 && o.Aggregate.Attacks[1].BreakdownBAB == 6 && o.Aggregate.Attacks[2].BreakdownBAB == 1,
                    "Rapid Shot's extra shot comes first at the highest bonus: +6, +6, +1");
                Assert(AttackMod(o.Aggregate.Attacks[0]) == singleMod - 2 && AttackMod(o.Aggregate.Attacks[2]) == singleMod - 7,
                    "Every shot takes -2 (PHB p.99)", $"single {singleMod}, shots {AttackMod(o.Aggregate.Attacks[0])}/{AttackMod(o.Aggregate.Attacks[2])}");
            }
        }
        finally
        {
            if (archer != null) archer.SetRapidShot(false);
            Cleanup(archer, target);
        }
    }

    /// <summary>The executor stops when the combat ends: before the first step, and after the step whose hook ended it.</summary>
    private static void TestStopsOnCombatEnd(GameManager gm)
    {
        CharacterController npc = null, target = null;
        GameManager.TurnPhase phase = gm.CurrentPhase;
        try
        {
            npc = CreateCombatant("SeqEnd", 6, Vector2Int.zero, ItemID.WeaponLongsword);
            target = CreateCombatant("SeqEndTarget", 4, Vector2Int.right, null);

            gm.Combat_SetPhase(GameManager.TurnPhase.CombatOver);
            AttackSequenceRequest before = AttackSequenceRequest.ForAI(gm, npc, target, null, null);
            before.CallerCanWait = false;
            var b = new AttackSequenceOutcome();
            gm.RunAttackSequenceSync(before, b);
            Assert(b.CombatEnded && b.AttacksMade == 0 && b.Stages.Count == 0 && npc.Actions.HasStandardAction,
                "Combat already over: no stage runs and nothing is spent", Join(b.Stages));
            gm.Combat_SetPhase(phase);

            // The combat ends inside the first step's post-attack hook: the second step of the full attack is never chosen.
            AttackSequenceRequest during = AttackSequenceRequest.ForAI(gm, npc, target, null, null);
            during.CallerCanWait = false;
            during.StageObserver = stage =>
            {
                if (stage == AttackSequenceStage.OnAttackResolved)
                    gm.Combat_SetPhase(GameManager.TurnPhase.CombatOver);
            };
            var d = new AttackSequenceOutcome();
            gm.RunAttackSequenceSync(during, d);
            Assert(d.CombatEnded && d.AttacksMade == 1
                && d.Stages.FindAll(s => s == AttackSequenceStage.ChooseNextStep).Count == 1,
                "The combat ended in the first step's hook: the sequence stops, the second step is never chosen",
                Join(d.Stages));
        }
        finally
        {
            gm.Combat_SetPhase(phase);
            Cleanup(npc, target);
        }
    }

    /// <summary>
    /// A shot that cannot be made declares nothing and spends nothing (stage 4's order for every caller, the Attack and Full
    /// Attack buttons included: GameManager.CanShootWithWeapon before TryDeclareRapidShotForAttack): an archer with Rapid
    /// Shot on and an empty quiver keeps its full-round action, its move action and its standard action.
    /// </summary>
    private static void TestRapidShotNotDeclaredWithoutAShot(GameManager gm)
    {
        CharacterController archer = null;
        try
        {
            archer = CreateCombatant("SeqRapidEmpty", 6, Vector2Int.zero, ItemID.WeaponLongbow, "Rapid Shot");
            archer.SetRapidShot(true);
            ItemData bow = archer.GetEquippedMainWeapon();
            Assert(!gm.CanShootWithWeapon(archer, bow, out string reason) && reason == "no ammunition",
                "An empty quiver fails the shared ranged weapon checks", reason);
            bool declared = gm.TryDeclareRapidShotForAttack(archer, bow, true);
            Assert(!declared && !archer.IsRapidShotDeclared && archer.Actions.HasFullRoundAction && archer.Actions.HasMoveAction
                && archer.Actions.HasStandardAction && !archer.ProgressiveAttackPool.HasStartedAttacking,
                "Rapid Shot with an empty quiver: nothing declared, no action spent",
                $"declared {declared}, full-round {archer.Actions.HasFullRoundAction}");
            GiveArrows(archer, 2);
            Assert(gm.TryDeclareRapidShotForAttack(archer, bow, true) && archer.IsRapidShotDeclared && !archer.Actions.HasFullRoundAction,
                "With arrows the declaration spends the full-round action (PHB p.99, D9)");
        }
        finally
        {
            if (archer != null) archer.SetRapidShot(false);
            Cleanup(archer);
        }
    }

    /// <summary>
    /// The Full Attack button after the Attack button paid the first step and no attack was made (a cancelled click): the
    /// turn becomes a full attack for the move action, as a second attack would (PHB p.143); after an attack it is refused.
    /// </summary>
    private static void TestFullAttackAfterPrepaidStep(GameManager gm)
    {
        CharacterController npc = null;
        try
        {
            npc = CreateCombatant("SeqPrepaid", 6, Vector2Int.zero, ItemID.WeaponLongsword);
            Assert(npc.TryPayForNextAttack(out _) && npc.ProgressiveAttackPool.PendingStepPaid && npc.Actions.HasMoveAction,
                "The Attack button's first step is paid with the standard action");
            Assert(npc.TryCommitFullAttackAction(out string why) && npc.ProgressiveAttackPool.IsFullAttack && !npc.Actions.HasMoveAction,
                "A paid but unmade first step may still become a full attack, for the move action", why);
            Assert(npc.CanCommitAttack(AttackStepKind.MainHand, out _), "The paid step can still be made");
        }
        finally
        {
            Cleanup(npc);
        }
    }

    /// <summary>
    /// A natural step lost to a failed ward save (D6) marks the attack pool, and the PC UI's natural-attack readers read the
    /// pool too (GameManager.IsNaturalAttackIndexUsedThisTurn, behind IsNaturalAttackSequenceIndexUsed and the button
    /// count), so the lost bite is not offered again.
    /// </summary>
    private static void TestWardLostNaturalStepStaysUsed(GameManager gm)
    {
        CharacterController beast = null;
        try
        {
            beast = CreateCombatant("SeqWardBeast", 4, Vector2Int.zero, null);
            beast.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
            beast.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = false });
            Assert(!gm.IsNaturalAttackIndexUsedThisTurn(beast, 0), "Before the step the bite is unused");
            GameManager.LoseNextNaturalAttack(beast);
            Assert(beast.ProgressiveAttackPool.IsNaturalAttackUsed(0) && gm.IsNaturalAttackIndexUsedThisTurn(beast, 0)
                && !gm.IsNaturalAttackIndexUsedThisTurn(beast, 1),
                "The step lost to the ward uses up the bite for the executor and the buttons alike; the claws stay unused");
        }
        finally
        {
            Cleanup(beast);
        }
    }

    /// <summary>
    /// Batch 13 step 17 (CMB-200; PHB p.140, p.274): a fighting-defensively declaration that no step used is dropped by the
    /// next attack button (GameManager.EndUnusedFightingDefensively), but a step paid and lost to a failed ward save was
    /// still attempted, so the round stays defensive though no swing was rolled (HasAttackedThisTurn stays false).
    /// </summary>
    private static void TestDefensiveRoundKeptAfterWardLostStep(GameManager gm)
    {
        CharacterController pc = null;
        try
        {
            pc = CreateCombatant("SeqDefWard", 6, Vector2Int.zero, ItemID.WeaponLongsword);
            pc.SetFightingDefensively(true);
            GameManager.EndUnusedFightingDefensively(pc);
            Assert(!pc.IsFightingDefensively, "A declaration no step used is dropped");

            pc.SetFightingDefensively(true);
            // The executor's ward branch: the step is paid and recorded, never rolled.
            Assert(pc.TryCommitAttack(AttackStepKind.MainHand, out _, out _) && !pc.HasAttackedThisTurn,
                "The ward-lost step is committed without a rolled attack");
            GameManager.EndUnusedFightingDefensively(pc);
            Assert(pc.IsFightingDefensively, "After a step lost to the ward the round stays defensive (PHB p.140, p.274)");
        }
        finally
        {
            Cleanup(pc);
        }
    }

    /// <summary>
    /// Batch 12 step 18 (PHB p.160, p.96): a two-weapon request (InterleaveOffHand, the Dual Wield full attack's and the
    /// AI's) runs the off-hand steps through the executor, capped by the off-hand count: a BAB +6 wielder with Two-Weapon
    /// Fighting only and one without the feat each make the main hand's two steps and exactly one off-hand step, in BAB
    /// order (main +6, off +6, main +1), the off-hand one at the off-hand ladder's BAB.
    /// </summary>
    private static void TestTwoWeaponStepsAndOffHandCap(GameManager gm)
    {
        foreach (bool withFeat in new[] { true, false })
        {
            CharacterController npc = null, target = null;
            try
            {
                npc = withFeat
                    ? CreateCombatant("SeqTwf", 6, Vector2Int.zero, ItemID.WeaponLongsword, "Two-Weapon Fighting")
                    : CreateCombatant("SeqTwfNoFeat", 6, Vector2Int.zero, ItemID.WeaponLongsword);
                npc.InventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponShortsword), EquipSlot.LeftHand);
                npc.InventoryComp.CharacterInventory.RecalculateStats();
                target = CreateCombatant(withFeat ? "SeqTwfTarget" : "SeqTwfNoFeatTarget", 4, Vector2Int.right, null);
                string who = withFeat ? "with Two-Weapon Fighting" : "without the feat";

                AttackSequenceRequest req = AttackSequenceRequest.ForAI(gm, npc, target, null, null);
                req.CallerCanWait = false;
                req.InterleaveOffHand = true;
                req.MainHandBabAdjustment = npc.GetDualWieldPenalties().mainPenalty;
                var o = new AttackSequenceOutcome();
                gm.RunAttackSequenceSync(req, o);
                List<string> labels = o.Aggregate.AttackLabels;
                int offSteps = labels.FindAll(l => l != null && l.StartsWith("Off hand")).Count;
                Assert(npc.GetOffHandAttackCount() == 1 && o.AttacksMade == 3 && offSteps == 1
                       && npc.ProgressiveAttackPool.OffHandStepsUsed == 1 && npc.ProgressiveAttackPool.MainHandStepsUsed == 2,
                    "A BAB +6 two-weapon full attack " + who + ": two main-hand steps and one off-hand step (the off-hand cap, PHB p.160)",
                    "attacks " + o.AttacksMade + " [" + string.Join(" | ", labels) + "] stop " + o.StopReason);
                // The main-hand steps' BAB carries the two-weapon main-hand penalty (MainHandBabAdjustment); the off-hand step's
                // is the off-hand ladder's, its penalty a separate term.
                int mainPenalty = npc.GetDualWieldPenalties().mainPenalty;
                Assert(labels.Count == 3 && labels[1] != null && labels[1].StartsWith("Off hand")
                       && o.Aggregate.Attacks[0].BreakdownBAB == 6 + mainPenalty && o.Aggregate.Attacks[1].BreakdownBAB == 6
                       && o.Aggregate.Attacks[2].BreakdownBAB == 1 + mainPenalty,
                    "The executor orders the steps by BAB, main hand on a tie: main +6, off +6, main +1 (" + who + ")",
                    "[" + string.Join(" | ", labels) + "]");
            }
            finally
            {
                Cleanup(npc, target);
            }
        }
    }

    // ── Fixtures ────────────────────────────────────────────────────

    private static CharacterController CreateCombatant(string name, int bab, Vector2Int position, ItemID? rightHand, params string[] feats)
    {
        var go = new GameObject($"AttackSequence_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();
        var stats = new CharacterStats(name, bab, "Fighter",
            16, 12, 14, 10, 10, 10,
            bab, 0, 0,
            8, 1, 0,
            6, 1, 300,
            "Human");
        if (feats != null && feats.Length > 0)
            stats.AddFeats(new List<string>(feats));
        controller.Init(stats, position, null, null);
        inventoryComp.Init(stats);
        if (rightHand.HasValue)
            inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(rightHand.Value), EquipSlot.RightHand);
        inventoryComp.CharacterInventory.RecalculateStats();
        return controller;
    }

    private static void GiveArrows(CharacterController c, int count)
    {
        ItemData arrows = ItemDatabase.CloneItem(ItemID.AmmoArrow);
        if (arrows == null || c.InventoryComp == null)
            return;
        arrows.Quantity = count;
        c.InventoryComp.CharacterInventory.AddItem(arrows);
    }

    private static int ArrowCount(CharacterController c)
        => c != null && c.InventoryComp != null ? c.InventoryComp.CharacterInventory.GetTotalAmmoCount(AmmunitionType.Arrow) : -1;

    private static int AttackMod(CombatResult result) => result != null ? result.TotalRoll - result.DieRoll : int.MinValue;

    private static void Cleanup(params CharacterController[] controllers)
    {
        foreach (CharacterController controller in controllers)
        {
            if (controller != null)
                Object.DestroyImmediate(controller.gameObject);
        }
    }
}
}
